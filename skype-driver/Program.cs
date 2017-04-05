using System;
using System.Diagnostics;
using System.IO;

namespace skype_driver
{
    class Program
    {
        static void Main(string[] args)
        {
            InitTraces();

            if (args.Length != 3)
            {
                usage();
                return;
            }


            try
            {
                using (var client = new SkypeForBusiness.Client())
                {
                    client.SignIn(args[0], args[1], args[2]);
                    var status = client.SignInStatus;

                    Trace.WriteLine(status.Item2);
                }
            }
            catch(Exception ex)
            {
                Trace.WriteLine($"[Exception] {ex.StackTrace}");
            }
            finally
            {
                Trace.Flush();
            }
        }

        private static void InitTraces()
        {
            Stream myFile = File.Create($@"c:\tmp\skype_driver_{Guid.NewGuid()}.log");

            TextWriterTraceListener myTextListener = new TextWriterTraceListener(myFile);
            Trace.Listeners.Add(myTextListener);
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        private static void usage()
        {
            Trace.WriteLine("Usage: ");
            Trace.WriteLine("  skype-driver signInAddress username password");
        }
    }
}
