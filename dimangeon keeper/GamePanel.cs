using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dimangeon_keeper
{
    public class GamePanel : Panel 
    {
        public GamePanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
