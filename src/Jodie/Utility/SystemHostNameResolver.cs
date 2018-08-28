using System;

namespace Jodie.Utility
{
    public class SystemHostNameResolver : IHostNameResolver
    {
        public string GetHostName()
        {
            return Environment.MachineName;
        }
    }

    public interface IHostNameResolver
    {
        string GetHostName();
    }
}
