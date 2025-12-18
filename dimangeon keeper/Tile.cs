using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dimangeon_keeper
{
    public class Tile
    {
        public TileType Type;
        public bool HasDigJob;

        public bool IsClaimed;
        public RoomType Room;


        public Tile(TileType type)
        {
            Type = type;
            Room = RoomType.None;
        }
    }
}
