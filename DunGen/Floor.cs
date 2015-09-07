using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DunGen {
  public class Floor {

    Dictionary<MapDirection, int> di = new Dictionary<MapDirection, int>() {
      { MapDirection.North, -1 },
      { MapDirection.South, 1 },
      { MapDirection.West, 0 },
      { MapDirection.East, 0 }
    };

    Dictionary<MapDirection, int> dj = new Dictionary<MapDirection, int>() {
      { MapDirection.North, 0 },
      { MapDirection.South, 0 },
      { MapDirection.West, -1 },
      { MapDirection.East, 1 }
    };

    List<MapDirection> dj_dirs = new List<MapDirection>() {
      MapDirection.North,
      MapDirection.South,
      MapDirection.West,
      MapDirection.East
    };

    Dictionary<MapDirection, MapDirection> opposite = new Dictionary<MapDirection, MapDirection>() {
      {MapDirection.North, MapDirection.South },
      {MapDirection.South, MapDirection.North },
      {MapDirection.West, MapDirection.East },
      {MapDirection.East, MapDirection.West }
    };

    Dictionary<MapDirection, Dictionary<string, int[,]>> close_end = new Dictionary<MapDirection, Dictionary<string, int[,]>>() {
    { MapDirection.North, new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{0,-1},{1,-1},{1,0},{1,1},{0,1}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{-1, 0}} }
      } },
    { MapDirection.South, new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{0,-1},{-1,-1},{-1,0},{-1,1},{0,1}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{1, 0}} }
      }},
    { MapDirection.West, new Dictionary<string, int[,]>() {
        { "walled", new int[,]{{-1,0},{-1,1},{0,1},{1,1},{1,0}} },
        { "close", new int[,]{{0,0}} },
        { "recurse", new int[,]{{0, -1}} }
      }},
    { MapDirection.East, new Dictionary<string, int[,]>() {
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
    {"corridor_change_chance", 50 },
    {"remove_deadends", 100 },
    {"add_stairs", 2 },
    {"map_style", "Standard" },
    {"cell_size",  18 }
  };

    public TileType[,] tiles;
    public List<Room> rooms;

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

      rooms = new List<Room>();
      roomIdForTile = new int[n_rows + 1, n_cols + 1];
      tiles = new TileType[n_rows + 1, n_cols + 1];

      InitTiles();
      PackRooms();
      OpenRooms();
      CreateCorridors();
      RemoveDeadends();
      FixDoors();
      EmptyBlocks();

      return tiles;
    }

    void InitTiles () {
      for (var r = 0; r <= n_rows; r++) {
        for (var c = 0; c <= n_cols; c++) {
          tiles[r, c] = TileType.Nothing;
        }
      }
    }

    void PackRooms () {

      for (var i = 0; i < n_i; i++) {
        var r = (i * 2) + 1;

        for (var j = 0; j < n_j; j++) {
          var c = (j * 2) + 1;

          if (tiles[r, c] == TileType.Room) {
            continue;
          }

          if ((i == 0 || j == 0) && Random.Range(0, 2) == 0) {
            continue;
          }

          var roomFootprint = new RoomFootprint();
          roomFootprint.row = i;
          roomFootprint.col = j;
          PlaceRoom(roomFootprint);
        }
      }
    }

    void PlaceRoom (RoomFootprint roomFootprint) {

      roomFootprint = SetRoom(roomFootprint);

      var r1 = (roomFootprint.row * 2) + 1;
      var c1 = (roomFootprint.col * 2) + 1;
      var r2 = ((roomFootprint.row + roomFootprint.height) * 2) - 1;
      var c2 = ((roomFootprint.col + roomFootprint.width) * 2) - 1;

      if (r1 < 1 || r2 > max_row) {
        return;
      }

      if (c1 < 1 || c2 > max_col) {
        return;
      }

      bool hit = RoomCollision(r1, c1, r2, c2);
      if (hit) {
        return;
      }

      int room_id = ++n_rooms;
      last_room_id = room_id;

      var room = new Room();
      room.tiles = new List<Vector3>();
      for (var r = r1; r <= r2; r++) {
        for (var c = c1; c <= c2; c++) {
          if (tiles[r, c] == TileType.Entrance) {
            //          _tiles[r, c] &= ~ ESPACE;
          } else if (tiles[r, c] == TileType.Perimeter) {

          } else {
            tiles[r, c] = TileType.Room;
            roomIdForTile[r, c] = room_id;
            room.tiles.Add(new Vector3(c, 0f, r));
          }
        }
      }

      room.id = room_id;
      room.row = r1;
      room.col = c1;
      room.northRow = r1;
      room.southRow = r2;
      room.westCol = c1;
      room.eastCol = c2;
      room.doors = new List<Door>();

      rooms.Add(room);

      // Perimeters
      for (var r = r1 - 1; r <= r2 + 1; r++) {
        if (tiles[r, c1 - 1] != TileType.Room && tiles[r, c1 - 1] != TileType.Entrance) {
          tiles[r, c1 - 1] = TileType.Perimeter;
        }
        if (tiles[r, c2 + 1] != TileType.Room && tiles[r, c2 + 1] != TileType.Entrance) {
          tiles[r, c2 + 1] = TileType.Perimeter;
        }
      }

      for (var c = c1 - 1; c <= c2 + 1; c++) {
        if (tiles[r1 - 1, c] != TileType.Room && tiles[r1 - 1, c] != TileType.Entrance) {
          tiles[r1 - 1, c] = TileType.Perimeter;
        }
        if (tiles[r2 + 1, c] != TileType.Room && tiles[r2 + 1, c] != TileType.Entrance) {
          tiles[r2 + 1, c] = TileType.Perimeter;
        }
      }
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

    bool RoomCollision (int r1, int c1, int r2, int c2) {
      for (int r = r1; r <= r2; r++) {
        for (int c = c1; c <= c2; c++) {

          if (tiles[r, c] == TileType.Blocked) {
            return true;
          }

          if (tiles[r, c] == TileType.Room) {
            return true;
          }
        }
      }

      return false;
    }

    void OpenRooms () {
      foreach (Room room in rooms) {
        OpenRoom(room);
      }
    }

    void OpenRoom (Room room) {

      var sills = DoorSills(room);
      // int n_opens = AllocateOpens(_tiles, room);
      // int n_opens = Random.Range (1, 3);
      int n_opens = 2;

      for (var i = 0; i < n_opens; i++) {
        var rand = Random.Range(0, sills.Count - 1);
        var sill = sills[rand];
        var door_r = sill.doorRow;
        var door_c = sill.doorCol;
        var door_cell = tiles[door_r, door_c];

        if (door_cell == TileType.Door) {
          --i;
          continue;
        }

        var out_id = sill.outRoomId;
        if (out_id > 0) {
          List<int> keyOrder = new List<int>() { room.id, out_id };
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

        var open_r = sill.row;
        var open_c = sill.col;
        MapDirection open_dir = sill.dir;

        for (var x = 0; x < 3; x++) {
          var r = open_r + (di[open_dir] * x);
          var c = open_c + (dj[open_dir] * x);
          tiles[r, c] = TileType.Entrance;
        }

        tiles[door_r, door_c] = TileType.Door;

        var door = new Door();
        door.row = door_r;
        door.col = door_c;
        door.outRoomId = out_id;
        door.openDir = open_dir;
        door.sill = sill;

        room.doors.Add(door);
      }
    }

    List<DoorSill> DoorSills (Room room) {

      var list = new List<DoorSill>();

      if (room.northRow >= 3) {
        for (var c = room.westCol; c <= room.eastCol; c += 2) {
          var sill = CheckSill(room, room.northRow, c, MapDirection.North);
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      if (room.southRow <= (n_rows - 3)) {
        for (var c = room.westCol; c <= room.eastCol; c += 2) {
          var sill = CheckSill(room, room.southRow, c, MapDirection.South);
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      if (room.westCol >= 3) {
        for (var r = room.northRow; r <= room.southRow; r += 2) {
          var sill = CheckSill(room, r, room.westCol, MapDirection.West);
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      if (room.eastCol <= n_cols - 3) {
        for (var r = room.northRow; r <= room.southRow; r += 2) {
          var sill = CheckSill(room, r, room.eastCol, MapDirection.East);
          if (sill != null) {
            list.Add(sill);
          }
        }
      }

      return list;
    }

    DoorSill CheckSill (Room room, int sill_r, int sill_c, MapDirection dir) {

      var door_r = sill_r + di[dir];
      var door_c = sill_c + dj[dir];

      var door_cell = tiles[door_r, door_c];
      if (door_cell != TileType.Perimeter) {
        return null;
      }

      var out_r = door_r + di[dir];
      var out_c = door_c + dj[dir];
      var out_cell = tiles[out_r, out_c];
      if (out_cell == TileType.Blocked) {
        return null;
      }

      var out_id = 0;
      if (out_cell == TileType.Room) {
        // get the room id of the cell and
        // return null if out is into the same room
        out_id = roomIdForTile[out_r, out_c];
        if (out_id == room.id) {
          return null;
        }
      }

      var sill = new DoorSill();
      sill.row = sill_r;
      sill.col = sill_c;
      sill.dir = dir;
      sill.doorRow = door_r;
      sill.doorCol = door_c;
      sill.outRoomId = out_id;
      
      return sill;
    }

    void CreateCorridors () {

      for (var i = 1; i < n_i; i++) {
        var r = (i * 2) + 1;
        for (var j = 1; j < n_j; j++) {
          var c = (j * 2) + 1;

          if (tiles[r, c] == TileType.Corridor) {
            continue;
          }

          CreateTunnel(i, j, MapDirection.North);
        }
      }
    }

    void CreateTunnel (int i, int j) {
      var dirs = TunnelDirections();

      foreach (MapDirection dir in dirs) {
        if (OpenTunnel(i, j, dir)) {
          var next_i = i + di[dir];
          var next_j = j + dj[dir];

          CreateTunnel(next_i, next_j, dir);
        }
      }
    }

    void CreateTunnel (int i, int j, MapDirection last_dir) {
      var dirs = TunnelDirections(last_dir);

      foreach (MapDirection dir in dirs) {
        if (OpenTunnel(i, j, dir)) {
          var next_i = i + di[dir];
          var next_j = j + dj[dir];

          CreateTunnel(next_i, next_j, last_dir);
        }
      }
    }

    List<MapDirection> TunnelDirections () {
      var copy = new List<MapDirection>(dj_dirs);
      var dirs = (List<MapDirection>)Shuffle(copy);
      return dirs;
    }

    List<MapDirection> TunnelDirections (MapDirection lastDirection) {
      var copy = new List<MapDirection>(dj_dirs);
      var dirs = (List<MapDirection>)Shuffle(copy);

      if (RollPercent((int)opts["corridor_change_chance"])) {
        dirs.Insert(0, lastDirection);
      }

      return dirs;
    }

    bool OpenTunnel (int i, int j, MapDirection dir) {
      var this_r = (i * 2) + 1;
      var this_c = (j * 2) + 1;
      var next_r = ((i + di[dir]) * 2) + 1;
      var next_c = ((j + dj[dir]) * 2) + 1;
      var mid_r = (this_r + next_r) / 2;
      var mid_c = (this_c + next_c) / 2;

      if (SoundTunnel(mid_r, mid_c, next_r, next_c)) {
        DelveTunnel(this_r, this_c, next_r, next_c);
        return true;
      }

      return false;
    }

    bool SoundTunnel (int mid_r, int mid_c, int next_r, int next_c) {

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
          if (tiles[r, c] == TileType.Blocked
              || tiles[r, c] == TileType.Perimeter
              || tiles[r, c] == TileType.Corridor
              ) {
            return false;
          }
        }
      }

      return true;
    }

    void DelveTunnel (int this_r, int this_c, int next_r, int next_c) {

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
          if (tiles[r, c] == TileType.Door) {
            cachedDoors.Add(new Vector2(c, r));
          }

          if (tiles[r, c] == TileType.Entrance) {
            cachedEntrances.Add(new Vector2(c, r));
          }

          tiles[r, c] = TileType.Corridor;
        }
      }
    }

    void RemoveDeadends () {
      CollapseTunnels();
    }

    void CollapseTunnels () {
      for (var i = 0; i < n_i; i++) {
        var r = i * 2 + 1;
        for (var j = 0; j < n_j; j++) {
          var c = j * 2 + 1;
          var test = tiles[r, c];
          if (test != TileType.Room && test != TileType.Corridor) {
            continue;
          }

          Collapse(r, c);
        }
      }
    }

    void Collapse (int r, int c) {

      var test = tiles[r, c];
      if (test != TileType.Room && test != TileType.Corridor) {
        return;
      }

      foreach (KeyValuePair<MapDirection, Dictionary<string, int[,]>> p in close_end) {

        if (CheckTunnel(r, c, p.Value)) {

          int[,] closeList = p.Value["close"];

          for (var x = 0; x < closeList.GetLength(0); x++) {
            var p1 = closeList[x, 0];
            var p2 = closeList[x, 1];

            tiles[r + p1, c + p2] = TileType.Nothing;
          }

          int[,] recurseList = p.Value["recurse"];
          var rr = r + recurseList[0, 0];
          var cr = c + recurseList[0, 1];

          if (tiles.GetLength(0) > rr && tiles.GetLength(1) > cr) {
            Collapse(rr, cr);
          }
        }
      }
    }

    bool CheckTunnel (int r, int c, Dictionary<string, int[,]> checkDirection) {
      int[,] list = checkDirection["walled"];
      for (var x = 0; x < list.GetLength(0); x++) {
        var p1 = list[x, 0];
        var p2 = list[x, 1];

        var test = tiles[r + p1, c + p2];
        if (test == TileType.Corridor || test == TileType.Room) {
          return false;
        }
      }

      return true;
    }

    void FixDoors () {

      //doorToRoomCache.AddRange(cachedDoors);

      foreach (Vector2 doorPos in cachedDoors) {
        tiles[(int)doorPos.y, (int)doorPos.x] = TileType.Door;
      }

      foreach (Vector2 entrancePos in cachedEntrances) {
        tiles[(int)entrancePos.y, (int)entrancePos.x] = TileType.Entrance;
      }

      for (var r = 0; r < tiles.GetLength(0); r++) {
        for (var c = 0; c < tiles.GetLength(1); c++) {
          var test = new Vector2(c, r);
          if (tiles[r, c] == TileType.Door) {
            if (!doorToRoomCache.Contains(test)) {
              //_tiles[r, c] = TileType.Nothing;
            }
          }
        }
      }
    }

    void EmptyBlocks () {
      // TODO: To implement
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

    bool RollPercent (float chance) {
      float rand = Random.Range(0f, 100f);
      return rand < chance;
    }

  }

}
