using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;

namespace Jodie.Utility
{
    public static class Retries
    {
        public static T Retry<T>(Func<T> function, TimeSpan retryInterval, int retryCount = 5)
        {
            var exceptions = new List<Exception>();

            for (var retry = 0; retry < retryCount; retry++)
            {
                try
                {
                    return function();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                Thread.Sleep(retryInterval);
            }

            throw exceptions.First();
        }

        public static T Retry<T>(this SqlCommand cmd, Func<T> function, TimeSpan retryInterval, int retryCount = 5)
        {
            return Retry(function, retryInterval, retryCount);
        }

        public static void Retry(Action action, TimeSpan retryInterval, int retryCount = 5)
        {
            var exceptions = new List<Exception>();

            for (var retry = 0; retry < retryCount; retry++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                Thread.Sleep(retryInterval);
            }

            throw exceptions.First();
        }
    }
}
