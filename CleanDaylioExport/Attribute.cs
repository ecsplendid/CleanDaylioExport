using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanDaylio
{
    public class Attribute
    {
        public string Name;

        public Dictionary<DateTime, string> Data 
            = new Dictionary<DateTime, string>();

    }
}
