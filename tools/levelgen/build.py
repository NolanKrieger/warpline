#!/usr/bin/env python3
"""Builds levels/*.lvl. Each level's developer route is levels/<id>.route (written by hand, checked by tests)."""
import os
from lvl import Lvl

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'levels')
H = 33          # every slice level fits the screen vertically: the camera only scrolls sideways
FLOOR = 29      # ground top


def boot():
    L = Lvl(150, H, "Boot", "A/D run · Space jump · S slide · jump off walls · LMB cyan, RMB magenta")
    L.border('#')
    L.rect(0, FLOOR, 149, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    L.rect(16, FLOOR - 2, 21, FLOOR - 1, 'X')          # a step (metal: dark surfaces never take portals)
    L.rect(30, FLOOR - 1, 34, FLOOR - 1, '^')          # spikes to hop
    L.rect(44, 2, 50, FLOOR - 2, '#')                  # wall with a one-tile tunnel at the floor: slide under
    L.rect(66, 8, 67, FLOOR - 4, 'X')                  # chimney: hanging metal pillar...
    L.rect(72, 18, 149, FLOOR, '#')                    # ...and the plateau face: wall-jump up between them
    L.rect(88, 17, 90, 17, '^')                        # spikes on the plateau
    L.rect(124, 13, 125, 17, '#')                      # barrier, too tall to jump
    L.rect(124, 16, 124, 17, '#')                      # portal panel at its foot (faces the player)
    L.rect(146, 2, 147, 17, '#')                       # far wall...
    L.rect(146, 3, 146, 10, '#')                       # ...with panels high up, visible over the barrier
    L.rect(131, 16, 131, 17, 'E')                      # exit door
    return L


def speedy():
    L = Lvl(160, H, "Speedy Thing", "Speed in, speed out. Fall into cyan, fly out of magenta. Slide (S) to keep speed.")
    L.border('#')
    L.rect(2, 2, 2, 12, '#')                           # back wall panels, facing right: the launcher
    L.rect(2, 13, 21, H - 1, '#')                      # start ledge
    L.put(6, 12, 'S')
    L.rect(22, FLOOR, 25, FLOOR, '#')                  # pit floor (panel)
    L.rect(22, FLOOR + 1, 25, H - 1, '#')
    L.rect(26, 16, 27, H - 1, '#')                     # pit's far wall, lower than the ledge
    L.rect(28, FLOOR, 35, H - 1, '#')
    L.rect(28, FLOOR - 1, 35, FLOOR - 1, '^')          # spike field
    L.rect(36, 22, 92, H - 1, '#')                     # landing platform
    L.rect(48, 2, 75, 20, '#')                         # long low tunnel: slide through it at speed
    L.rect(93, FLOOR - 1, 114, FLOOR - 1, '^')         # the gap: only a kept fling clears it
    L.rect(93, FLOOR, 114, H - 1, '#')
    L.rect(115, 22, 157, H - 1, '#')                   # far side
    L.rect(120, 2, 157, 12, '#')                       # low ceiling over the far side
    L.rect(150, 20, 150, 21, 'E')
    return L


def up_and_over():
    L = Lvl(140, H, "Up and Over", "What goes down comes back up. Grills wipe your portals.")
    L.border('#')
    L.rect(2, 8, 20, H - 1, '#')                       # high start
    L.put(5, 7, 'S')
    L.rect(21, FLOOR, 109, H - 1, '#')                 # ground
    L.rect(21, FLOOR, 34, FLOOR, '#')                  # drop zone (panel floor)
    L.rect(35, FLOOR - 1, 45, FLOOR - 1, '^')
    L.rect(46, FLOOR, 57, FLOOR, '#')                  # launch pad (panel floor)
    L.rect(58, FLOOR - 1, 60, FLOOR - 1, '^')
    L.rect(61, 10, 79, H - 1, '#')                     # tower
    L.rect(70, 2, 70, 9, ':')                          # fizzler grill across the tower top
    L.rect(82, FLOOR, 86, FLOOR, '#')                  # second drop zone
    L.rect(87, FLOOR - 1, 104, FLOOR - 1, '^')
    L.rect(105, FLOOR, 109, FLOOR, '#')                # second launch pad
    L.rect(110, 12, 137, H - 1, '#')                   # exit tower
    L.rect(130, 10, 130, 11, 'E')
    return L


def loop():
    L = Lvl(160, H, "Terminal", "Floor and ceiling make a loop. Fall until you can't fall faster, then aim the exit.")
    L.border('#')
    L.rect(2, 2, 3, H - 1, '#')
    L.rect(3, 3, 3, 9, '#')                            # launcher wall (faces right)
    L.rect(5, 1, 8, 1, '#')                            # ceiling panels
    L.rect(2, FLOOR, 39, H - 1, '#')
    L.rect(5, FLOOR, 8, FLOOR, '#')                    # floor panels
    L.put(6, FLOOR - 1, 'S')
    L.rect(16, 12, 17, FLOOR - 1, '#')                 # chamber wall
    L.rect(18, FLOOR - 1, 39, FLOOR - 1, '^')          # spike field
    L.rect(40, 22, 97, H - 1, '#')                     # landing platform
    L.rect(52, 2, 52, 21, ':')                         # grill
    L.rect(88, 2, 89, 17, '#')                         # chimney pillar
    L.rect(94, 8, 97, 21, '#')                         # wall to climb
    L.rect(98, FLOOR, 157, H - 1, '#')
    L.rect(100, FLOOR, 106, FLOOR, '#')                # drop zone
    L.rect(107, FLOOR - 1, 128, FLOOR - 1, '^')
    L.rect(129, FLOOR, 134, FLOOR, '#')                # launch pad
    L.rect(135, 11, 157, H - 1, '#')                   # exit tower
    L.rect(150, 9, 150, 10, 'E')
    return L


def gatekeeper():
    L = Lvl(150, H, "Gatekeeper", "Buttons open doors. Some stay open, some close on a timer, some only while you stand on them.")
    L.border('#')
    L.rect(0, FLOOR, 149, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. latch: a wall with a door, the button just before it
    L.rect(20, 2, 21, FLOOR - 1, '#')
    L.obj("door d1 x=20 y=20 w=2 h=9")
    L.obj("button b1 x=14 y=28 opens=d1 mode=latch")
    # 2. timed race: the button far from its door, a hop on the way
    L.obj("button b2 x=30 y=28 opens=d2 mode=timed time=3.9")
    L.rect(48, FLOOR - 1, 50, FLOOR - 1, '^')
    L.rect(70, 2, 71, FLOOR - 1, '#')
    L.obj("door d2 x=70 y=23 w=2 h=6")
    # 3. hold: stand on the button to open the door, shoot a portal through, walk into the other one
    L.obj("button b3 x=85 y=28 opens=d3 mode=hold")
    L.rect(88, FLOOR, 90, FLOOR, '#')                  # floor panels next to the button
    L.rect(95, 2, 96, FLOOR - 1, '#')
    L.obj("door d3 x=95 y=22 w=2 h=7")
    L.rect(125, 2, 147, FLOOR - 1, '#')                # far wall...
    L.rect(125, 25, 125, 28, '#')                      # ...with panels at floor level
    L.rect(104, 27, 104, 28, 'E')
    return L


def beamline(phases=(172 / 120, 98 / 120, 25 / 120, 143 / 120)):   # a full-speed run meets every gate off
    L = Lvl(140, H, "Beamline", "Red kills. Beams go through portals too: send one into the receiver to open the exit.")
    L.border('#')
    L.rect(0, FLOOR, 139, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. laser gates under a low ceiling: dashed lines show where a beam will be
    L.rect(16, 2, 51, 19, '#')
    for i, x in enumerate((22, 30, 38, 46)):
        L.obj(f"laser g{i + 1} x={x} y=20 dir=down period=1.6 on=0.8 phase={phases[i]:.5f}")
    # 2. the redirect: emitter on the left, a pillar with panels on both faces, receiver across the room
    L.rect(52, 2, 55, 12, '#')
    L.obj("laser l5 x=56 y=8 dir=right")
    L.rect(80, 2, 81, 12, '#')
    L.rect(80, 8, 80, 9, '#')                          # left face, in line with the beam
    L.rect(81, 4, 81, 5, '#')                          # right face, in line with the receiver
    L.obj("receiver r1 x=120 y=4 opens=d1")
    L.rect(125, 2, 126, FLOOR - 1, '#')
    L.obj("door d1 x=125 y=22 w=2 h=7")
    L.rect(132, 27, 132, 28, 'E')
    return L


def crossfire(phase=0.0):
    L = Lvl(150, H, "Crossfire", "Hop the bullets. Bullets go through portals too, and they press buttons.")
    L.border('#')
    L.rect(0, FLOOR, 149, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. hurdles: a turret in a low step fires along the floor
    L.rect(60, 27, 63, 28, '#')
    L.put(60, 28, '.')
    L.obj(f"turret t1 x=60 y=28 dir=left period=1.3 speed=16 phase={phase:.5f}")
    L.rect(64, 2, 149, 12, '#')                        # ceiling over the second hall
    # 2. return to sender: a turret fires across the hall at head height into a panel wall;
    #    a pillar with a panel on its far face lines up with a button on a ledge nobody can reach.
    L.rect(66, 13, 67, 23, '#')                        # pillar from the ceiling; the turret hangs under it
    L.obj("turret t2 x=67 y=24 dir=right period=0.6 speed=18")
    L.rect(84, 20, 85, 26, '#')                        # short wall the stream hits
    L.rect(84, 24, 84, 25, '#')
    L.rect(92, 13, 93, 16, '#')                        # pillar hanging from the ceiling
    L.rect(93, 13, 93, 14, '#')                        # its right face, level with the button
    L.rect(106, 14, 111, 14, '#')                      # high ledge
    L.obj("button b1 x=108 y=13 opens=d1 mode=latch")
    L.rect(125, 13, 126, FLOOR - 1, '#')
    L.obj("door d1 x=125 y=23 w=2 h=6")
    L.rect(138, 27, 138, 28, 'E')
    return L


def afterburner():
    L = Lvl(170, H, "Afterburner", "Orange gel: run to top speed. Carry that speed through a portal to go up.")
    L.border('#')
    L.rect(0, FLOOR, 169, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    L.rect(2, FLOOR, 45, FLOOR, '=')                   # runway 1
    L.rect(46, FLOOR, 59, H - 2, '.')                  # a pit only gel speed clears...
    L.rect(46, H - 2, 59, H - 2, '^')                  # ...with spikes at the bottom
    L.rect(78, FLOOR, 118, FLOOR, '=')                 # runway 2
    L.rect(119, 2, 167, FLOOR - 1, '#')                # end wall: portal it at floor level...
    L.rect(66, 24, 72, 24, '#')                        # ...and come up out of the floor onto this ledge
    L.rect(68, 22, 68, 23, 'E')
    return L


def rebound():
    L = Lvl(170, H, "Rebound", "Purple gel bounces you back. Jump as you land to go higher. Hold S to stick.")
    L.border('#')
    L.rect(0, FLOOR, 169, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    L.rect(12, FLOOR, 15, FLOOR, '%')                  # pad
    L.rect(18, 25, 21, FLOOR - 1, '#')                 # a wall too tall to jump
    L.rect(22, 25, 29, FLOOR - 1, '#')
    # the shaft: bounce floor, climb out the top by jumping on every landing
    L.rect(30, FLOOR, 37, FLOOR, '%')
    L.rect(38, 16, 60, FLOOR - 1, '#')                 # high ground to the right of the shaft
    # islands over spikes: keep bouncing, don't touch the floor
    L.rect(61, 17, 113, FLOOR - 1, '#')
    L.rect(61, 16, 113, 16, '^')
    for x in (67, 75, 83, 90, 98, 106):                # a hop is ~7.8 tiles at run speed
        L.rect(x, 16, x + 2, 16, '%')
    L.rect(114, 16, 167, FLOOR - 1, '#')
    L.rect(124, 14, 124, 15, 'E')
    return L


def moving_parts():
    L = Lvl(160, H, "Moving Parts", "Panels on rails carry you and your portals. Jump off a rising lift to keep its speed.")
    L.border('#')
    L.rect(0, FLOOR, 159, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. the lift: a shaft of metal, a high ledge you can only reach riding up
    L.rect(19, 2, 19, 22, 'X')                         # shaft wall hanging from the ceiling (metal: no portal shortcut up)
    L.obj("mover lift x=20 y=28 w=4 h=1 to=20,12 period=4.4 phase=2.95")   # waits at the bottom for a full-speed run
    L.rect(24, 12, 60, FLOOR - 1, 'X')                 # high ground (metal sides)
    L.rect(25, 12, 60, 12, '#')                        # ...with a panel top
    # 2. the ferry over a long pit; its top takes portals, the far tower does not
    L.rect(61, FLOOR - 1, 125, FLOOR - 1, '^')
    L.obj("mover ferry x=62 y=16 w=5 h=1 to=118,16 period=7")
    L.rect(126, 16, 157, FLOOR - 1, 'X')               # exit tower (metal)
    L.rect(140, 14, 140, 15, 'E')
    return L


def packs():
    """Extra level packs: tools/levelgen/pack_*.py, each with LEVELS = [(id, fn), ...]."""
    import glob, importlib
    out = []
    for path in sorted(glob.glob(os.path.join(os.path.dirname(__file__), 'pack_*.py'))):
        mod = importlib.import_module(os.path.splitext(os.path.basename(path))[0])
        out += list(mod.LEVELS)
    return out


def main():
    for lid, fn in packs() + [("01-boot", boot), ("02-speedy", speedy), ("03-up", up_and_over), ("04-terminal", loop),
                    ("05-gatekeeper", gatekeeper), ("06-beamline", beamline), ("07-crossfire", crossfire),
                    ("08-afterburner", afterburner), ("09-rebound", rebound), ("10-moving", moving_parts)]:
        fn().save(os.path.join(OUT, lid + ".lvl"))
        print("wrote", lid)


if __name__ == '__main__':
    main()
