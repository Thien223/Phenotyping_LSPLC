using System;

namespace LSPLC.Utilities
{
    public class Log
    {
        public Log(string targetClass)
        {
            TargetClass = targetClass;
        }
        private string TargetClass { get; set; }
        private string Time { get; set; }
        public void Write(object message)
        {
            var now = DateTime.Now;
            Time = string.Format(@"{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}", now.Year,now.Month,now.Day,now.Hour,now.Minute,now.Second);
            Console.WriteLine($"[{Time}] - [{TargetClass}]: {message.ToString()}");
        }

    }
}