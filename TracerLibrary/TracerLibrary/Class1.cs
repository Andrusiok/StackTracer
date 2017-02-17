using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Linq;

namespace TracerLibrary
{
    internal class TraceResultElement
    {
        internal int id { get; private set; }
        internal int paramsCount { get; private set; }

        internal string name { get; private set; }
        internal string package { get; private set; }
        internal string type { get; private set; }

        internal LinkedList<TraceResultElement> childs = null;
        internal TraceResultElement parent = null;
        internal long time = 0;

        private Stopwatch timer = new Stopwatch();

        internal TraceResultElement(string type, int id = -1, int paramsCount = -1, string name = "undefined", string package = "undefined")
        {
            this.id = id;
            this.paramsCount = paramsCount;
            this.name = name;
            this.package = package;
            this.type = type;
        }

        internal void AddElement(TraceResultElement element)
        {
            if (childs == null) childs = new LinkedList<TraceResultElement> { };
            childs.AddLast(element);
            element.parent = this;
        }

        internal void StartTimer()
        {
            timer.Reset();
            timer.Start();
        }

        internal void stopTimer()
        {
            timer.Stop();
            time = timer.ElapsedMilliseconds;
        }
    }

    public class Tracer
    {
        private object thisLock = new Object();
        private TraceResultElement currentElement;
        private TraceResultElement runningElement;
        private XDocument document;
        internal TraceResultElement root;

        public Tracer()
        {
            lock (thisLock)
            {
                root = new TraceResultElement("root");
                currentElement = root;
                Console.WriteLine("Начало работы");
            }
        }
        // метод должен быть вызван перед вызовом замеряемого метода
        public void StartTrace()
        {
            lock (thisLock)
            {
                FindMethod();
            }
        }
        // метод должен быть вызван после вызова замеряемого метода
        public void StopTrace()
        {
            try
            {     
            lock(thisLock)
            {
                runningElement = FindStartedMethod(Thread.CurrentThread.ManagedThreadId);
                runningElement.stopTimer();

                long tmpTime = runningElement.time;
                runningElement = runningElement.parent;
                if (runningElement.parent == root) runningElement.time += tmpTime;
            }
            }
            catch
            {
                Console.WriteLine("Ошибка остановки");
            }
        }
        // построение xml-файла с результатами замеров
        public void BuildXml()
        {
            document = new XDocument();
            string fileName = "ResultXml.xml";

            XElement xroot = new XElement("root");
            document.Add(xroot);
            xroot=BuildXMLTree(root, xroot);

            document.Save("ResultXML.xml");

        }

        private XElement BuildXMLTree(TraceResultElement pointer, XElement e)
        {
            LinkedListNode<TraceResultElement> tmpNode;
            XElement element = e;
            XElement subj = null;

            for (tmpNode = pointer.childs.First; tmpNode != null; tmpNode = tmpNode.Next)
            {
                switch (tmpNode.Value.type)
                {
                    case "thread":
                        subj = new XElement("thread");
                        subj.Add(new XAttribute("time", tmpNode.Value.time));
                        subj.Add(new XAttribute("id", tmpNode.Value.id));                        
                        break;
                    case "method":
                        subj = new XElement("method");
                        subj.Add(new XAttribute("time", tmpNode.Value.time));
                        subj.Add(new XAttribute("package", tmpNode.Value.package));
                        subj.Add(new XAttribute("paramscount", tmpNode.Value.paramsCount));                      
                        subj.Add(new XAttribute("name", tmpNode.Value.name));
                        break;
                }
                if (tmpNode.Value.childs != null) element.Add(BuildXMLTree(tmpNode.Value, subj));
                else element.Add(subj);
            }
            return element;
        }
        // вывод дерева результатов на консоль
        public void PrintToConsole()
        {
            Console.WriteLine("root");
            BuildConsoleTree(root, 1);
        }

        private void BuildConsoleTree(TraceResultElement pointer, int d)
        {
            LinkedListNode<TraceResultElement> tmpNode;
            int depth = d;

            for (tmpNode = pointer.childs.First; tmpNode != null; tmpNode = tmpNode.Next)
            {
                for (int i = 0; i < depth; i++) Console.Write("-");
                switch (tmpNode.Value.type)
                {
                    case "thread":
                        Console.WriteLine("thread id={0}, time={1}ms", tmpNode.Value.id, tmpNode.Value.time);
                        break;
                    case "method":
                        Console.WriteLine("method name={0}, time={1}ms, package={2}, paramscount={3}", tmpNode.Value.name, tmpNode.Value.time, tmpNode.Value.package, tmpNode.Value.paramsCount);
                        break;
                }
                if (tmpNode.Value.childs != null)
                {
                    depth+=2;
                    BuildConsoleTree(tmpNode.Value, depth);
                    depth-=2;
                }
            }
        }

        private void FindMethod()
        {
            currentElement = FindStartedMethod(Thread.CurrentThread.ManagedThreadId);

            try
            {
                StackTrace stackTrace = new StackTrace();
                StackFrame frame;

                for (int i = 0; i < stackTrace.FrameCount; i++)
                {
                    frame = stackTrace.GetFrame(i);
                    Type declaringType = frame.GetMethod().DeclaringType;
                    if (declaringType != null)
                    {
                        string name = declaringType.Name;
                        if (name != typeof(Tracer).Name)
                        {
                            TraceResultElement tmpElement = new TraceResultElement("method", -1, frame.GetMethod().GetParameters().Count(), frame.GetMethod().Name, frame.GetMethod().Module.Name);
                            currentElement.AddElement(tmpElement);
                            currentElement = tmpElement;
                            currentElement.StartTimer();
                            break;
                        }

                    }

                }
            }
            catch
            {
                Console.WriteLine("Ошибка");
            }
        }

        private TraceResultElement FindStartedMethod(int id)
        {
            TraceResultElement tmpThread = GetPointer(id);

            if (tmpThread.childs == null) return tmpThread;
            else return GetRecentMethod(tmpThread, null);
        }

        private TraceResultElement GetRecentMethod(TraceResultElement pointer, TraceResultElement found)
        {
            LinkedListNode<TraceResultElement> tmpNode;
            TraceResultElement foundElement = found;

            for (tmpNode = pointer.childs.First; tmpNode != null; tmpNode = tmpNode.Next)
            {
                if (tmpNode.Value.time == 0 && (tmpNode.Next == null || tmpNode.Next.Value.time != 0))
                {
                    foundElement = tmpNode.Value;
                    if (tmpNode.Value.childs == null) break;
                    else foundElement = GetRecentMethod(tmpNode.Value, foundElement);

                }
            }

            return foundElement;
        }

        private bool CheckForThreads(int id)
        {
            if (root.childs != null) return true;
            return false;
        }

        private TraceResultElement GetPointer(int id)
        {
            if (CheckForThreads(id) == true)
            {
                LinkedListNode<TraceResultElement> tmpNode;

                for (tmpNode = root.childs.First; tmpNode != null; tmpNode = tmpNode.Next)
                {
                    if (tmpNode.Value.id == id) return tmpNode.Value;
                }
            }

            currentElement = new TraceResultElement("thread", id);
            root.AddElement(currentElement);
            return currentElement;

        }

    }
}