using UnityEngine;
using System.Collections;

namespace DunGen {

  public class Door {

    public MapDirection wallDir;
    public MapDirection openDir;
    public int row;
    public int col;
    public int outRoomId;
    public DoorSill sill;

  }
}
