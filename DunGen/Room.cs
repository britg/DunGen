using UnityEngine;
using System.Collections;

namespace DunGen {
  public class Room {

    public int id;
    public int row;
    public int col;
    public int northRow;
    public int southRow;
    public int westCol;
    public int eastCol;



  }
}


//var room = new Hashtable() {
//      {"id", room_id}, {"row", r1}, {"col", c1},
//      {"north", r1}, {"south", r2}, {"west", c1}, {"east", c2},
//      {"doors", new Dictionary<string, List<Hashtable>>() {
//          {"north", new List<Hashtable>()},
//          {"south", new List<Hashtable>()},
//          {"west", new List<Hashtable>()},
//          {"east", new List<Hashtable>()}
//        } }
//    };
