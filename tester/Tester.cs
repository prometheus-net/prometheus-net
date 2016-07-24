using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prometheus;

namespace tester
{
    abstract class Tester
    {
        public virtual void OnStart()
        {
            
        }

        public virtual void OnObservation()
        {
            
        }

        public virtual void OnEnd()
        {
            
        }

        public abstract IMetricServer InitializeMetricHandler();
    }
}
