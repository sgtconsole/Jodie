using System;

namespace Jodie.Utility
{
    public class SystemClock : IClock
    {
        public DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        public DateTime GetNow()
        {
            return DateTime.Now;
        }
    }

    public interface IClock
    {
        DateTime GetUtcNow();
        DateTime GetNow();
    }
}
