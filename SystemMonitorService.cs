using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Black_Hole_Cross
{
    public class SystemMonitorService
    {
        private static SystemMonitorService _instance;
        public static SystemMonitorService Instance => _instance ?? (_instance = new SystemMonitorService());

        private readonly DispatcherTimer _timer;

        public event Action OnTick;

        private SystemMonitorService()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            OnTick?.Invoke();
        }
        public void SetInterval(int seconds)
        {
            bool wasRunning = _timer.IsEnabled;
            _timer.Stop();
            _timer.Interval=TimeSpan.FromSeconds(seconds);

            if(wasRunning || seconds > 0)
            {
                _timer.Start();
            }
        }

        public void Stop() => _timer.Stop();
        public void Start() => _timer.Start();
    }
}
