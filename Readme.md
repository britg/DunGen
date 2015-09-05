# DunGen

A simple dungeon floor generator in C#. Inspired by the work of donjon http://donjon.bin.sh/

### Scope

The scope of this script is to simply deliver a two dimensional array of flags describing the foundation of a dungeon floor. That means just the room, corridor, door, and wall cells. From there, other information can be added that is game-related. 

### Current Status

Still in the process of porting.

### Usage

```
	var floor = new DunGen.Floor();
	DunGen.TileType[,] = floor.GenerateTiles();

```

Tiles is a two dimensional array of `DunGen.TileType`. Possible `TileType`s:

- Nothing - an empty cell
- Room - room interior cell
- Corridor - a hallway cell
- Perimeter - margin around a room
- Entrance - space before a door in the direction it 'opens'

Note that `Corridor` cells can cut through rooms.