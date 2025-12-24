using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dimangeon_keeper
{
    public class DigJob
    {
        public int X { get; } 
        public int Y { get; } 
        
        public bool IsCancelled { get; private set; }
        public bool IsAssigned { get; private set; }
        public Creature AssignedTo { get; private set; }

        public DigJob(int x, int y )
        {
            X = x;
            Y = y;
        }

        public void Cancel()
        {
            IsCancelled = true;
        }

        public bool TryAssign(Creature c)
        {
            if (IsCancelled || IsAssigned)
            {
                return false;
            }
            IsAssigned = true;
            AssignedTo = c;
            
            return true;
        }
        //мне пофи у кого отменять, поэтому в параметр не передаю чела
        public void Unassign()
        {
            IsAssigned = false;
            AssignedTo = null;
        }
    }
}
