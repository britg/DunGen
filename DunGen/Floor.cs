using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DunGen {
  public class Floor {

    int[,] dungeonLayout = new int[,] { { 1, 1, 1, }, { 1, 0, 1 }, { 1, 1, 1 } };
    int corridorLayout = 50;

    Dictionary<string, int> di = new Dictionary<string, int>() {
    { "north", -1 }, { "south", 1 }, {"west", 0 }, {"east", 0 }
  };

    Dictionary<string, int> dj = new Dictionary<string, int>() {
    { "north", 0 }, { "south", 0 }, {"west", -1 }, {"east", 1 }
  };

    List<string> dj_dirs = new List<string>() { "north", "south", "west", "east" };

    Dictionary<string, string> opposite = new Dictionary<string, string>() {
    {"north", "south" },
    {"south", "north" },
    {"west", "east" },
    {"east", "west" }
  };

    Dictionary<string, Dictionary<string, int[,]>> close_end = new Dictionary<string, Dictionary<string, int[,]>>() {
    { "north", new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{0,-1},{1,-1},{1,0},{1,1},{0,1}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{-1, 0}} }
      } },
    { "south", new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{0,-1},{-1,-1},{-1,0},{-1,1},{0,1}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{1, 0}} }
      }},
    { "west", new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{-1,0},{-1,1},{0,1},{1,1},{1,0}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{0, -1}} }
      }},
    { "east", new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{-1,0},{-1,-1},{0,-1},{1,-1},{1,0}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{0, 1}} }
      }}
    };

    static Hashtable defaultOpts = new Hashtable() {
    {"seed", (int)Time.time},
    {"n_rows", 79 },
    {"n_cols", 79 },
    {"dungeon_layout", "None" },
    {"room_min", 9 },
    {"room_max", 17 },
    {"room_layout", "Packed" },
    {"corridor_layout", "Bent" },
    {"remove_deadends", 100 },
    {"add_stairs", 2 },
    {"map_style", "Standard" },
    {"cell_size",  18 }
  };

    TileType[,] tiles;
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
    int[,] roomIdForTile;
    Dictionary<string, int> roomConnections = new Dictionary<string, int>();
    List<Vector2> cachedDoors = new List<Vector2>();
    List<Vector2> cachedEntrances = new List<Vector2>();
    List<Vector2> doorToRoomCache = new List<Vector2>();

    public static Floor Create () {
      return Create(defaultOpts);
    }

    public static Floor Create (Hashtable _opts) {
      var floor = new Floor();
      floor.Generate(_opts);
      return floor;
    }

    public TileType[,] Generate () {
      return Generate(defaultOpts);
    }

    public TileType[,] Generate (Hashtable _opts) {
      // TODO: Merge default opts with opts;
      opts = defaultOpts;

      //Random.seed = (int)opts["seed"];

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
      roomIdForTile = new int[n_rows + 1, n_cols + 1];
      tiles = new TileType[n_rows + 1, n_cols + 1];

      tiles = InitTiles(tiles);
      tiles = PackRooms(tiles);
      tiles = OpenRooms(tiles, rooms);
      tiles = CreateCorridors(tiles);
      tiles = Cleanup(tiles);

      return tiles;
    }

    TileType[,] InitTiles (TileType[,] _tiles) {

      for (var r = 0; r <= n_rows; r++) {
        for (var c = 0; c <= n_cols; c++) {
          _tiles[r, c] = TileType.Nothing;
        }
      }

      return _tiles;
    }

    TileType[,] PackRooms (TileType[,] _tiles) {

      for (var i = 0; i < n_i; i++) {
        var r = (i * 2) + 1;

        for (var j = 0; j < n_j; j++) {
          var c = (j * 2) + 1;

          if (_tiles[r, c] == TileType.Room) {
            continue;
          }

          if ((i == 0 || j == 0) && Random.Range(0, 2) == 0) {
            continue;
          }

          var roomFootprint = new RoomFootprint();
          roomFootprint.row = i;
          roomFootprint.col = j;
          _tiles = PlaceRoom(_tiles, roomFootprint);

        }
      }

      return _tiles;
    }

    TileType[,] PlaceRoom (TileType[,] _tiles, RoomFootprint roomFootprint) {

      roomFootprint = SetRoom(roomFootprint);

      var r1 = (roomFootprint.row * 2) + 1;
      var c1 = (roomFootprint.col * 2) + 1;
      var r2 = ((roomFootprint.row + roomFootprint.height) * 2) - 1;
      var c2 = ((roomFootprint.col + roomFootprint.width) * 2) - 1;

      if (r1 < 1 || r2 > max_row) {
        return _tiles;
      }

      if (c1 < 1 || c2 > max_col) {
        return _tiles;
      }

      bool hit = RoomCollision(_tiles, r1, c1, r2, c2);
      if (hit) {
        return _tiles;
      }

      int room_id = ++n_rooms;
      last_room_id = room_id;

      for (var r = r1; r <= r2; r++) {
        for (var c = c1; c <= c2; c++) {
          if (_tiles[r, c] == TileType.Entrance) {
            //          _tiles[r, c] &= ~ ESPACE;
          } else if (_tiles[r, c] == TileType.Perimeter) {

          } else {
            _tiles[r, c] = TileType.Room;
            roomIdForTile[r, c] = room_id;
          }
        }
      }

      var room = new Hashtable() {
      {"id", room_id}, {"row", r1}, {"col", c1},
      {"north", r1}, {"south", r2}, {"west", c1}, {"east", c2},
      {"doors", new Dictionary<string, List<Hashtable>>() {
          {"north", new List<Hashtable>()},
          {"south", new List<Hashtable>()},
          {"west", new List<Hashtable>()},
          {"east", new List<Hashtable>()}
        } }
    };

      rooms[room_id] = room;

      // Perimeters
      for (var r = r1 - 1; r <= r2 + 1; r++) {
        if (_tiles[r, c1 - 1] != TileType.Room && _tiles[r, c1 - 1] != TileType.Entrance) {
          _tiles[r, c1 - 1] = TileType.Perimeter;
        }
        if (_tiles[r, c2 + 1] != TileType.Room && _tiles[r, c2 + 1] != TileType.Entrance) {
          _tiles[r, c2 + 1] = TileType.Perimeter;
        }
      }

      for (var c = c1 - 1; c <= c2 + 1; c++) {
        if (_tiles[r1 - 1, c] != TileType.Room && _tiles[r1 - 1, c] != TileType.Entrance) {
          _tiles[r1 - 1, c] = TileType.Perimeter;
        }
        if (_tiles[r2 + 1, c] != TileType.Room && _tiles[r2 + 1, c] != TileType.Entrance) {
          _tiles[r2 + 1, c] = TileType.Perimeter;
        }
      }

      return _tiles;
    }

    RoomFootprint SetRoom (RoomFootprint roomFootprint) {

      bool heightDefined = roomFootprint.height != 0;
      bool widthDefined = roomFootprint.width != 0;
      bool rowDefined = roomFootprint.row != 0;
      bool colDefined = roomFootprint.col != 0;

      if (!heightDefined) {
        if (rowDefined) {
          var a = n_i - room_base - roomFootprint.row;
          if (a < 0) {
            a = 0;
          }

          var r = (a < room_radix) ? a : room_radix;
          roomFootprint.height = (int)Random.Range(0, r) + room_base;
        } else {
          roomFootprint.height = (int)Random.Range(0, room_radix) + room_base;
        }
      }

      if (!widthDefined) {
        if (colDefined) {
          var a = n_j - room_base - roomFootprint.col;
          if (a < 0) {
            a = 0;
          }

          var r = (a < room_radix) ? a : room_radix;
          roomFootprint.width = (int)Random.Range(0, r) + room_base;
        } else {
          roomFootprint.width = (int)Random.Range(0, room_radix) + room_base;
        }
      }

      if (!rowDefined) {
        roomFootprint.row = Random.Range(0, n_i - roomFootprint.height);
      }

      if (!colDefined) {
        roomFootprint.col = Random.Range(0, n_j - roomFootprint.width);
      }

      return roomFootprint;
    }

    bool RoomCollision (TileType[,] _tiles, int r1, int c1, int r2, int c2) {

      for (int r = r1; r <= r2; r++) {
        for (int c = c1; c <= c2; c++) {

          if (_tiles[r, c] == TileType.Blocked) {
            return true;
          }

          if (_tiles[r, c] == TileType.Room) {
            return true;
          }
        }
      }

      return false;
    }

    TileType[,] OpenRooms (TileType[,] _tiles, Hashtable _rooms) {

      foreach (DictionaryEntry entry in _rooms) {
        var room = (Hashtable)entry.Value;

        _tiles = OpenRoom(_tiles, room);
      }

      return _tiles;
    }

    TileType[,] OpenRoom (TileType[,] _tiles, Hashtable room) {

      var sills = DoorSills(_tiles, room);
      // int n_opens = AllocateOpens(_tiles, room);
      // int n_opens = Random.Range (1, 3);
      int n_opens = 2;

      for (var i = 0; i < n_opens; i++) {
        var rand = Random.Range(0, sills.Count - 1);
        var sill = sills[rand];
        var door_r = (int)sill["door_r"];
        var door_c = (int)sill["door_c"];
        var door_cell = _tiles[door_r, door_c];

        if (door_cell == TileType.Door) {
          --i;
          continue;
        }

        var out_id = (int)sill["out_id"];
        if (out_id > 0) {
          List<int> keyOrder = new List<int>() { (int)room["id"], out_id };
          keyOrder.Sort();
          var key = string.Format("{0},{1}", keyOrder[0], keyOrder[1]);
          if (roomConnections.ContainsKey(key)) {
            roomConnections[key] += 1;
            continue;
          } else {
            roomConnections[key] = 1;
            doorToRoomCache.Add(new Vector2(door_c, door_r));
          }
        }

        var open_r = (int)sill["sill_r"];
        var open_c = (int)sill["sill_c"];
        string open_dir = (string)sill["dir"];

        for (var x = 0; x < 3; x++) {
          var r = open_r + (di[open_dir] * x);
          var c = open_c + (dj[open_dir] * x);
          _tiles[r, c] = TileType.Entrance;
        }

        _tiles[door_r, door_c] = TileType.Door;
        var door = new Hashtable();
        door["row"] = door_r;
        door["col"] = door_c;
        if (out_id > 0) {
          door["out_id"] = out_id;
        }

        ((Dictionary<string, List<Hashtable>>)room["doors"])[open_dir].Add(door);
      }

      return _tiles;
    }

    List<Hashtable> DoorSills (TileType[,] _tiles, Hashtable room) {

      var list = new List<Hashtable>();

      if ((int)room["north"] >= 3) {
        for (var c = (int)room["west"]; c <= (int)room["east"]; c += 2) {
          var sill = CheckSill(_tiles, room, (int)room["north"], c, "north");
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      if ((int)room["south"] <= (n_rows - 3)) {
        for (var c = (int)room["west"]; c <= (int)room["east"]; c += 2) {
          var sill = CheckSill(_tiles, room, (int)room["south"], c, "south");
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      if ((int)room["west"] >= 3) {
        for (var r = (int)room["north"]; r <= (int)room["south"]; r += 2) {
          var sill = CheckSill(_tiles, room, r, (int)room["west"], "west");
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      if ((int)room["east"] <= n_cols - 3) {
        for (var r = (int)room["north"]; r <= (int)room["south"]; r += 2) {
          var sill = CheckSill(_tiles, room, r, (int)room["east"], "east");
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      return list;
    }

    Hashtable CheckSill (TileType[,] _tiles, Hashtable room, int sill_r, int sill_c, string dir) {

      var door_r = sill_r + di[dir];
      var door_c = sill_c + dj[dir];

      var door_cell = _tiles[door_r, door_c];
      if (door_cell != TileType.Perimeter) {
        return null;
      }

      var out_r = door_r + di[dir];
      var out_c = door_c + dj[dir];
      var out_cell = _tiles[out_r, out_c];
      if (out_cell == TileType.Blocked) {
        return null;
      }

      var out_id = 0;
      if (out_cell == TileType.Room) {
        // get the room id of the cell and
        // return null if out is into the same room
        out_id = roomIdForTile[out_r, out_c];
        if (out_id == (int)room["id"]) {
          return null;
        }
      }

      var sill = new Hashtable() {
      {"sill_r", sill_r},
      {"sill_c", sill_c},
      {"dir", dir},
      {"door_r", door_r},
      {"door_c", door_c},
      {"out_id", out_id} // temp
    };

      return sill;
    }

    int AllocateOpens (TileType[,] _tiles, Hashtable room) {

      var room_h = (((int)room["south"] - (int)room["north"]) / 2) + 1;
      var room_w = (((int)room["east"] - (int)room["west"]) / 2) + 1;
      var f = (int)Mathf.Sqrt((float)room_w * (float)room_h);

      return (f + (int)Random.Range(0, f));
    }

    TileType[,] CreateCorridors (TileType[,] _tiles) {

      for (var i = 1; i < n_i; i++) {
        var r = (i * 2) + 1;
        for (var j = 1; j < n_j; j++) {
          var c = (j * 2) + 1;

          if (_tiles[r, c] == TileType.Corridor) {
            continue;
          }

          _tiles = CreateTunnel(_tiles, i, j, null);
        }
      }
      return _tiles;
    }

    TileType[,] CreateTunnel (TileType[,] _tiles, int i, int j, string last_dir) {
      var dirs = TunnelDirections(_tiles, last_dir);

      foreach (string dir in dirs) {
        if (OpenTunnel(ref _tiles, i, j, dir)) {
          var next_i = i + di[dir];
          var next_j = j + dj[dir];

          _tiles = CreateTunnel(_tiles, next_i, next_j, last_dir);
        }
      }

      return _tiles;
    }

    List<string> TunnelDirections (TileType[,] _tiles, string lastDirection) {
      var dirs = (List<string>)Shuffle(dj_dirs);

      if (lastDirection != null) {
        if (tpd.RollPercent(corridorLayout)) {
          dirs.Insert(0, lastDirection);
        }
      }

      return dirs;
    }

    bool OpenTunnel (ref TileType[,] _tiles, int i, int j, string dir) {
      var this_r = (i * 2) + 1;
      var this_c = (j * 2) + 1;
      var next_r = ((i + di[dir]) * 2) + 1;
      var next_c = ((j + dj[dir]) * 2) + 1;
      var mid_r = (this_r + next_r) / 2;
      var mid_c = (this_c + next_c) / 2;

      if (SoundTunnel(_tiles, mid_r, mid_c, next_r, next_c)) {
        _tiles = DelveTunnel(_tiles, this_r, this_c, next_r, next_c);
        return true;
      }

      return false;
    }

    bool SoundTunnel (TileType[,] _tiles, int mid_r, int mid_c, int next_r, int next_c) {

      if (next_r < 0 || next_r > n_rows) {
        return false;
      }

      if (next_c < 0 || next_c > n_cols) {
        return false;
      }

      var rList = new List<int>() { mid_r, next_r };
      rList.Sort();
      var r1 = rList[0];
      var r2 = rList[1];

      var cList = new List<int>() { mid_c, next_c };
      cList.Sort();
      var c1 = cList[0];
      var c2 = cList[1];

      for (var r = r1; r <= r2; r++) {
        for (var c = c1; c <= c2; c++) {
          if (_tiles[r, c] == TileType.Blocked
              || _tiles[r, c] == TileType.Perimeter
              || _tiles[r, c] == TileType.Corridor
              //            || _tiles[r, c] == TileType.Room
              ) {
            return false;
          }
        }
      }

      return true;
    }

    TileType[,] DelveTunnel (TileType[,] _tiles, int this_r, int this_c, int next_r, int next_c) {

      var rList = new List<int>() { this_r, next_r };
      rList.Sort();
      var r1 = rList[0];
      var r2 = rList[1];

      var cList = new List<int>() { this_c, next_c };
      cList.Sort();
      var c1 = cList[0];
      var c2 = cList[1];

      for (var r = r1; r <= r2; r++) {
        for (var c = c1; c <= c2; c++) {
          if (_tiles[r, c] == TileType.Door) {
            cachedDoors.Add(new Vector2(c, r));
          }

          if (_tiles[r, c] == TileType.Entrance) {
            cachedEntrances.Add(new Vector2(c, r));
          }

          _tiles[r, c] = TileType.Corridor;
        }
      }

      return _tiles;
    }

    int[,] EmplaceStairs (int[,] _tiles) {

      return _tiles;
    }

    TileType[,] Cleanup (TileType[,] _tiles) {
      _tiles = RemoveDeadends(_tiles);
      _tiles = FixDoors(_tiles);
      _tiles = EmptyBlocks(_tiles);
      return _tiles;
    }

    TileType[,] RemoveDeadends (TileType[,] _tiles) {
      return CollapseTunnels(_tiles);
    }

    TileType[,] CollapseTunnels (TileType[,] _tiles) {
      for (var i = 0; i < n_i; i++) {
        var r = i * 2 + 1;
        for (var j = 0; j < n_j; j++) {
          var c = j * 2 + 1;
          var test = _tiles[r, c];
          if (test != TileType.Room && test != TileType.Corridor) {
            continue;
          }

          _tiles = Collapse(_tiles, r, c);
        }
      }

      return _tiles;
    }

    TileType[,] Collapse (TileType[,] _tiles, int r, int c) {

      var test = _tiles[r, c];
      if (test != TileType.Room && test != TileType.Corridor) {
        return _tiles;
      }

      foreach (KeyValuePair<string, Dictionary<string, int[,]>> p in close_end) {

        if (CheckTunnel(_tiles, r, c, p.Value)) {

          int[,] closeList = p.Value["close"];

          for (var x = 0; x < closeList.GetLength(0); x++) {
            var p1 = closeList[x, 0];
            var p2 = closeList[x, 1];

            _tiles[r + p1, c + p2] = TileType.Nothing;
          }

          int[,] recurseList = p.Value["recurse"];
          var rr = r + recurseList[0, 0];
          var cr = c + recurseList[0, 1];

          if (_tiles.GetLength(0) > rr && _tiles.GetLength(1) > cr) {
            _tiles = Collapse(_tiles, rr, cr);
          }
        }
      }

      return _tiles;
    }

    bool CheckTunnel (TileType[,] _tiles, int r, int c, Dictionary<string, int[,]> checkDirection) {
      int[,] list = checkDirection["walled"];
      for (var x = 0; x < list.GetLength(0); x++) {
        var p1 = list[x, 0];
        var p2 = list[x, 1];

        var test = _tiles[r + p1, c + p2];
        if (test == TileType.Corridor || test == TileType.Room) {
          return false;
        }
      }

      return true;
    }

    TileType[,] FixDoors (TileType[,] _tiles) {

      //doorToRoomCache.AddRange(cachedDoors);

      foreach (Vector2 doorPos in cachedDoors) {
        _tiles[(int)doorPos.y, (int)doorPos.x] = TileType.Door;
      }

      foreach (Vector2 entrancePos in cachedEntrances) {
        _tiles[(int)entrancePos.y, (int)entrancePos.x] = TileType.Entrance;
      }

      for (var r = 0; r < _tiles.GetLength(0); r++) {
        for (var c = 0; c < _tiles.GetLength(1); c++) {
          var test = new Vector2(c, r);
          if (_tiles[r, c] == TileType.Door) {
            if (!doorToRoomCache.Contains(test)) {
              //_tiles[r, c] = TileType.Nothing;
            }
          }
        }
      }

      return _tiles;
    }

    TileType[,] EmptyBlocks (TileType[,] _tiles) {

      return _tiles;
    }

    public IList<T> Shuffle<T>(IList<T> list) {
      int n = list.Count;
      while (n > 1) {
        n--;
        int k = Random.Range(0, n + 1);
        T value = list[k];
        list[k] = list[n];
        list[n] = value;
      }
      return list;
    }

  }

}
