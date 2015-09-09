using UnityEngine;
using System.Collections;

namespace DunGen {
  public class FloorOptions {

    public int seed;
    public int numRows;
    public int numCols;
    public int roomSizeMin;
    public int roomSizeMax;
    public int corridorBendChance;
    public bool removeDeadEnds;

    public static FloorOptions defaults {
      get {
        var opts = new FloorOptions();
        opts.seed = (int)Time.time;
        opts.numRows = 79;
        opts.numCols = 79;
        opts.roomSizeMin = 9;
        opts.roomSizeMax = 17;
        opts.corridorBendChance = 50;
        opts.removeDeadEnds = true;
        return opts;
      }
    }

  }
}

//static Hashtable defaultOpts = new Hashtable() {
//    {"seed", (int)Time.time},
//    {"n_rows", 79 },
//    {"n_cols", 79 },
//    {"dungeon_layout", "None" },
//    {"room_min", 9 },
//    {"room_max", 17 },
//    {"room_layout", "Packed" },
//    {"corridor_change_chance", 50 },
//    {"remove_deadends", 100 },
//    {"add_stairs", 2 },
//    {"map_style", "Standard" },
//    {"cell_size",  18 }
//  };