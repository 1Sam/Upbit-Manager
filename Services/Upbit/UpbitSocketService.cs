using System;
using System.Threading.Tasks;

namespace Upbit_Manager.Services.Upbit
{
    public class UpbitSocketService
    {
        // Placeholder for websocket-based real-time data
        public event Action<double,double,string,string> OnTrade; // price, vol, side, market

        public Task RunAsync(string[] markets)
        {
            // TODO: implement socket connection
            return Task.CompletedTask;
        }
    }
}
