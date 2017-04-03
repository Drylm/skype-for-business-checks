using System;

namespace skype_driver
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                using (var client = new SkypeForBusiness.Client())
                {
                    client.SignIn(args[0], args[1], args[2]);
                    var status = client.SignInStatus;

                    Console.WriteLine(status.Item2);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[Exception] {ex.StackTrace}");
            }
        }
    }
}
