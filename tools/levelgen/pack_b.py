"""Levels 16-20. Loaded by build.py (LEVELS = [(id, fn), ...])."""
from lvl import Lvl

H = 33
FLOOR = 29


def trampoline():
    L = Lvl(150, H, "Trampoline", "Bounce up into one portal, fall out of a higher one. Hold S on purple to stand still.")
    L.border('#')
    L.rect(0, FLOOR, 149, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. the pump: a gel pad, half of it under a low block, the rest open to the ceiling
    L.rect(16, FLOOR, 32, FLOOR, '%')
    L.rect(16, 21, 22, 22, '#')                        # low block over the left half of the pad
    L.rect(36, 6, 147, FLOOR - 1, '#')                 # the high ledge: 23 tiles up
    # 2. a hold door on the ledge; its button sits on gel, so crouch to hold it
    L.rect(69, 6, 74, 6, '%')
    L.obj("button b1 x=71 y=5 opens=d1 mode=hold")
    L.obj("door d1 x=90 y=2 w=2 h=4")
    L.rect(118, 2, 147, 5, '#')                        # end wall of the room behind the door
    L.rect(104, 4, 104, 5, 'E')
    return L


def ferris():
    L = Lvl(160, H, "Ferris", "Portal onto a car from above, ride it round, jump off at the top with its speed.")
    L.border('#')
    L.rect(0, FLOOR, 159, H - 1, '#')
    L.rect(2, 8, 38, FLOOR - 1, '#')                   # high start
    L.put(5, 7, 'S')
    L.rect(44, 0, 159, 1, 'X')                         # metal ceiling past the start: no dropping in from above
    # 1. the big wheel over a spike pit: four cars, a quarter turn apart
    L.rect(39, FLOOR - 1, 71, FLOOR - 1, '^')
    for i in range(4):
        L.obj(f"mover c{i + 1} x=67 y=18 w=4 h=1 orbit=9 period=8 phase={i * 2}")
    L.rect(72, 13, 100, FLOOR - 1, 'X')                # the ledge the wheel lifts you to (metal)
    L.rect(95, 13, 100, 13, '#')                       # ...a panel strip at its far end
    # 2. a small wheel out over the void
    L.rect(101, FLOOR - 1, 130, FLOOR - 1, '^')
    for i in range(2):
        L.obj(f"mover w{i + 1} x=122 y=16 w=4 h=1 orbit=7 period=6 phase={i * 3}")
    L.rect(131, 12, 157, FLOOR - 1, 'X')               # exit platform
    L.rect(143, 10, 143, 11, 'E')
    return L


def piston(phases=(67 / 120, 215 / 120, 122 / 120, 30 / 120, 178 / 120)):   # a full-speed run finds every piston up
    L = Lvl(170, H, "Piston", "Pistons crush, panels block lasers. Run the gaps, then keep under the shield.")
    L.border('#')
    L.rect(0, FLOOR, 169, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. the crusher corridor: metal floor and ceiling, pistons slam down on a rhythm
    L.rect(14, 2, 74, 15, 'X')
    L.rect(14, FLOOR, 74, FLOOR, 'X')
    for i, x in enumerate((22, 32, 42, 52, 62)):
        L.obj(f"mover p{i + 1} x={x} y=16 w=3 h=6 to={x},23 period=2 phase={phases[i]:.5f} kind=metal")
    # 2. the laser field: always-on beams, a sliding metal shield to hide under
    L.rect(80, 2, 130, 11, 'X')                        # ceiling holding the emitters
    L.rect(80, FLOOR, 130, FLOOR, 'X')                 # metal floor (no portal skip under the beams)
    for i, x in enumerate(range(90, 122, 4)):
        L.obj(f"laser z{i + 1} x={x} y=12 dir=down")
    L.obj("mover shield x=82 y=19 w=6 h=1 to=121,19 period=9 phase=3.2 kind=metal")
    L.rect(150, 27, 150, 28, 'E')
    return L


def undertow():
    L = Lvl(150, H, "Undertow", "Grills wipe your portals. Rebuild them on the way down, one on the moving panel.")
    L.border('#')
    L.rect(0, FLOOR, 149, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. the chimney: wall-jump up between two walls, through two grills
    L.rect(19, 2, 19, 24, '#')                         # left wall hangs from the ceiling (walk in under it)
    L.rect(24, 6, 60, FLOOR - 1, '#')                  # right wall = the shelf you climb onto
    L.rect(20, 20, 23, 20, ':')
    L.rect(20, 12, 23, 12, ':')
    # 2. the drop: a grill curtain, then spikes, a sliding panel, a far floor and a tower
    L.rect(61, 2, 61, 28, ':')
    L.rect(62, FLOOR - 1, 94, FLOOR - 1, '^')
    L.obj("mover raft x=64 y=22 w=4 h=1 to=88,22 period=4 phase=0.787")
    L.rect(106, 15, 147, FLOOR - 1, '#')               # the tower (its top is 14 up from the floor)
    L.rect(106, 16, 106, FLOOR - 1, 'X')               # metal face: no wall-portal shortcut
    L.rect(125, 13, 125, 14, 'E')
    return L


def finale(gates=(36 / 120, 154 / 120, 80 / 120), turret_phase=0.69, ferry_phase=2.9):
    L = Lvl(230, H, "Warpline", "Everything you've learned, one run. Find your own line through it.")
    L.border('#')
    L.rect(0, FLOOR, 229, H - 1, '#')
    L.put(4, FLOOR - 1, 'S')
    # 1. sprint: speed gel into a pit you can only clear at gel speed (or portal over)
    L.rect(3, FLOOR, 34, FLOOR, '=')
    L.rect(35, FLOOR, 48, H - 2, '.')
    L.rect(35, H - 2, 48, H - 2, '^')
    # 2. the gate: a timed button, laser gates under a low ceiling, the door
    L.obj("button b1 x=52 y=28 opens=d1 mode=timed time=4.2")
    L.rect(58, 2, 88, 19, '#')
    for i, x in enumerate((62, 70, 78)):
        L.obj(f"laser g{i + 1} x={x} y=20 dir=down period=1.6 on=0.8 phase={gates[i]:.5f}")
    L.rect(92, 2, 93, FLOOR - 1, '#')
    L.obj("door d1 x=92 y=23 w=2 h=6")
    # 3. the bounce pit: jump on every landing to climb out
    L.rect(94, FLOOR, 119, FLOOR, '%')
    L.rect(120, 12, 227, FLOOR - 1, '#')               # the high ground
    L.rect(120, 11, 121, 11, '#')                      # a lip at its edge stops the turret's bullets
    # 4. turret hurdles on the high ground; the turret hangs under a pillar at the gap's edge
    L.rect(166, 2, 167, 10, '#')
    L.obj(f"turret t1 x=166 y=11 dir=left period=1.3 speed=16 phase={turret_phase:.5f}")
    # 5. the gap and the ferry
    L.rect(168, 12, 191, FLOOR - 1, '.')
    L.rect(168, FLOOR - 1, 191, FLOOR - 1, '^')
    L.obj(f"mover ferry x=168 y=12 w=4 h=1 to=188,12 period=5 phase={ferry_phase:.5f}")
    L.rect(214, 10, 214, 11, 'E')
    return L


LEVELS = [("16-trampoline", trampoline), ("17-ferris", ferris), ("18-piston", piston), ("19-undertow", undertow),
          ("20-warpline", finale)]
