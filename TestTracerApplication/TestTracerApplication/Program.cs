using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using TracerLibrary;

namespace TestTracerApplication
{
    class Program
    {
        static Tracer tracer = new Tracer();

        static void Main(string[] args)
        {
            tracer.StartTrace();
            SomeFunc();

            var th = new Thread(SomeFunc);
            th.Start();

            Thread.Sleep(1000);

            SomeFunc();

            tracer.StopTrace();
            tracer.PrintToConsole();
            tracer.BuildXml();

            Console.ReadLine();
        }

        static void SomeFunc()
        {
            tracer.StartTrace();
            Thread.Sleep(100);
            AnyFunc();
            tracer.StopTrace();
        }

        static void AnyFunc()
        {
            tracer.StartTrace();
            Thread.Sleep(100);
            tracer.StopTrace();
        }
    }
}
