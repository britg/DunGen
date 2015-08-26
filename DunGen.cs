using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DunGen {

  public enum TileType {
    Nothing,
    Blocked,
    Room,
    Corridor,
    Perimeter,
    Entrance,
    Door,
    StairUp,
    StairDown
  }

  int[,] dungeonLayout = new int[,] { { 1, 1, 1, }, { 1, 0, 1 }, { 1, 1, 1 } };
  int corridorLayout = 50;

  Dictionary<string, int> di = new Dictionary<string, int>() {
    { "north", -1 }, { "south", 1 }, {"west", 0 }, {"east", 0 }
  };

  Dictionary<string, int> dj = new Dictionary<string, int>() {
    { "north", 0 }, { "south", 0 }, {"west", -1 }, {"east", 1 }
  };

  Dictionary<string, string> opposite = new Dictionary<string, string>() {
    {"north", "south" },
    
    {"west", "east" },
    {"east", "west" }
  };

  Hashtable defaultOpts = new Hashtable() {
    {"n_rows", 39 },
    {"n_cols", 39 },
    {"dungeon_layout", "None" },
    {"room_min", 5 },
    {"room_max", 9 },
    {"room_layout", "Scattered" },
    {"corridor_layout", "Bent" },
    {"remove_deadends", 100 },
    {"add_stairs", 2 },
    {"map_style", "Standard" },
    {"cell_size",  18 }
  };

  TileType[,] cells;
  Hashtable rooms;
  Hashtable opts;
  int n_i;
  int n_j;
  int n_rows;
  int n_cols;
  int max_row;
  int max_col;
  int n_rooms;
  int room_min;
  int room_max;
  int room_base;
  int room_radix;
  int last_room_id = 0;

  Hashtable proto;

  public TileType[,] CreateDungeon () {
    return CreateDungeon(defaultOpts);
  }

  public TileType[,] CreateDungeon (Hashtable _opts) {
    // TODO: Merge default opts with opts;
    opts = defaultOpts;

    n_i = (int)((int)opts["n_rows"] / 2);
    n_j = (int)((int)opts["n_cols"] / 2);
    n_rows = n_i * 2;
    n_cols = n_j * 2;
    max_row = n_rows - 1;
    max_col = n_cols - 1;
    n_rooms = 0;
    room_min = (int)opts["room_min"];
    room_max = (int)opts["room_max"];
    room_base = (int)((room_min + 1) / 2);
    room_radix = (int)((room_max - room_min) / 2) + 1;

    rooms = new Hashtable();
    cells = new TileType[n_rows+1, n_cols+1];
    cells = InitCells(cells);
    cells = PackRooms(cells);

    Debug.Log (cells);

    return cells;
  }

  TileType[,] InitCells (TileType[,] _cells) {

    for (var r = 0; r <= n_rows; r++) {
      for (var c = 0; c <= n_cols; c++) {
        _cells[r,c] = TileType.Nothing;
      }
    }

    return _cells;
  }

  TileType[,] PackRooms (TileType[,] _cells) {

    for (var i = 0; i < n_i; i++) {
      var r = (i * 2) + 1;

      for (var j = 0; j < n_j; j++) {
        var c = (j * 2) + 1;

        if (_cells[r,c] == TileType.Room) {
          continue;
        }

        if ((i == 0 || j == 0) && Random.Range(0, 2) == 0) {
          continue;
        }

        Hashtable proto = new Hashtable() {
          {"i", i }, {"j", j }
        };
        _cells = PlaceRoom(_cells, proto);

      }
    }

    return _cells;
  }

  TileType[,] PlaceRoom (TileType[,] _cells, Hashtable proto) {

    proto = SetRoom(proto);

    var r1 = ((int)proto["i"] * 2) + 1;
    var c1 = ((int)proto["j"] * 2) + 1;
    var r2 = (((int)proto["i"] + (int)proto["height"]) * 2) - 1;
    var c2 = (((int)proto["j"] + (int)proto["width"]) * 2) - 1;

    if (r1 < 1 || r2 > max_row) {
      return _cells;
    }

    if (c1 < 1 || c2 > max_col) {
      return _cells;
    }

    bool hit = RoomCollision(_cells, r1, c1, r2, c2);
    if (hit) {
      return _cells;
    }

    int room_id = ++n_rooms;
    last_room_id = room_id;

    for (var r = r1; r <= r2; r++) {
      for (var c = c1; c <= c2; c++) {
        if (_cells[r, c] == TileType.Entrance) {
//          _cells[r, c] &= ~ ESPACE;
        } else if (_cells[r, c] == TileType.Perimeter) {

        }
        _cells[r, c] = TileType.Room;
      }
    }

    rooms[room_id] = new Hashtable () {
      {"id", room_id}, {"row", r1}, {"col", c1},
      {"north", r1}, {"south", r2}, {"west", c1}, {"east", c2}
    };

    // Perimeters
    for (var r = r1-1; r <= r2+1; r++) {
      if (_cells[r, c1-1] != TileType.Room && _cells[r, c1-1] != TileType.Entrance) {
        _cells[r, c1-1] = TileType.Perimeter;
      }
      if (_cells[r, c2+1] != TileType.Room && _cells[r, c2+1] != TileType.Entrance) {
        _cells[r, c2+1] = TileType.Perimeter;
      }
    }

    for (var c = c1-1; c <= c2+1; c++) {
      if (_cells[r1-1, c] != TileType.Room && _cells[r1-1, c] != TileType.Entrance) {
        _cells[r1-1, c] = TileType.Perimeter;
      }
      if (_cells[r2+1, c] != TileType.Room && _cells[r2+1, c] != TileType.Entrance) {
        _cells[r2+1, c] = TileType.Perimeter;
      }
    }

    return _cells;
  }

  Hashtable SetRoom (Hashtable _proto) {

    bool heightDefined = _proto.ContainsKey("height");
    bool widthDefined = _proto.ContainsKey("width");
    bool iDefined = _proto.ContainsKey("i");
    bool jDefined = _proto.ContainsKey("j");

    if (!heightDefined) {
      if (iDefined) {
        var a = n_i - room_base - (int)_proto["i"];
        if (a < 0) {
          a = 0;
        }

        var r = (a < room_radix) ? a : room_radix;
        _proto["height"] = (int)Random.Range(0, r) + room_base;
      } else {
        _proto["height"] = (int)Random.Range(0, room_radix) + room_base;
      }
    }

    if (!widthDefined) {
      if (jDefined) {
        var a = n_j - room_base - (int)_proto["j"];
        if (a < 0) {
          a = 0;
        }

        var r = (a < room_radix) ? a : room_radix;
        _proto["width"] = (int)Random.Range(0, r) + room_base;
      } else {
        _proto["width"] = (int)Random.Range(0, room_radix) + room_base;
      }
    }

    if (!iDefined) {
      _proto["i"] = Random.Range(0, n_i - (int)_proto["height"]);
    }

    if (!jDefined) {
      _proto["j"] = Random.Range(0, n_j - (int)_proto["width"]);
    }

    return _proto;
  }

  bool RoomCollision (TileType[,] _cells, int r1, int c1, int r2, int c2) {

    for (int r = r1; r <= r2; r++) {
      for (int c = c1; c <= c2; c++) {

        if (_cells[r, c] == TileType.Blocked) {
          return true;
        }

        if (_cells[r, c] == TileType.Room) {
          return true;
        }
      }
    }

    return false;
  }

  TileType[,] OpenRooms (TileType[,] _cells, Hashtable _rooms) {

    foreach (DictionaryEntry entry in _rooms) {
      var room_id = (int)entry.Key;
      var room = (Hashtable)entry.Value;

      _cells = OpenRoom(_cells, room);
    }

    return _cells;
  }

  TileType[,] OpenRoom (TileType[,] _cells, Hashtable room) {

    return _cells;
  }

  int[,] LabelRooms (int[,] _cells) {

    return _cells;
  }

  int[,] Corridors (int[,] _cells) {

    return _cells;
  }

  int[,] EmplaceStairs (int[,] _cells) {

    return _cells;
  }

  int[,] CleanDungeon (int[,] _cells) {

    return _cells;
  }


}