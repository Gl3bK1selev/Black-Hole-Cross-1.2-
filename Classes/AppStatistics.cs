using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 
    using System.Text.Json;
namespace Black_Hole_Cross
{
   

    public class AppStatistics
    {
     
        public int LaunchCount { get; set; } = 0;
        public double TotalUsageTime { get; set; } = 0; 
       
   
        public DateTime FirstLaunch { get; set; } = DateTime.Now;
        public DateTime LastLaunch { get; set; } = DateTime.Now;
      
    }
}
