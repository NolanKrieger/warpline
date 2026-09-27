"""Level pack A: levels 11-15 (Relay, Slipstream, Lockstep, Ricochet, Overdrive)."""
from lvl import Lvl

H = 33
FLOOR = 29


def relay():
    L = Lvl(200, H, "Relay", "Fling, then pass it on: while you fly, move both portals onto the pad below.")
    L.border('#')
    L.rect(0, FLOOR, 199, H - 1, '#')
    # 1. launcher: plateau, pit, back wall high above the plateau
    L.rect(2, 6, 10, FLOOR - 1, '#')
    L.put(5, 5, 'S')
    L.rect(15, FLOOR - 1, 43, FLOOR - 1, '^')
    # 2. the relay pad: too short to land on at fling speed, perfect for a floor-to-floor portal pair
    L.rect(44, 27, 53, FLOOR - 1, '#')
    L.rect(54, FLOOR - 1, 107, FLOOR - 1, '^')
    # 3. a high ledge, only reachable on the rebound
    L.rect(108, 8, 150, FLOOR - 1, '#')
    # 4. the canyon: drop off the ledge onto the patch, come back out of the ledge's far face
    L.rect(151, FLOOR - 1, 156, FLOOR - 1, '^')
    L.rect(157, 27, 168, FLOOR - 1, '#')
    L.rect(169, FLOOR - 1, 171, FLOOR - 1, '^')
    L.rect(172, 20, 197, FLOOR - 1, '#')
    L.rect(190, 18, 190, 19, 'E')
    return L


def slipstream():
    L = Lvl(185, H, "Slipstream", "Build speed on the gel, then slide (S) through the tunnels: slides keep speed.")
    L.border('#')
    L.rect(0, FLOOR, 184, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    L.rect(2, FLOOR, 44, FLOOR, '=')                   # runway
    L.rect(45, 18, 74, FLOOR - 2, '#')                 # tunnel 1: one tile tall, 30 long
    L.rect(76, FLOOR, 84, H - 3, '.')                  # a pit only a kept slide clears...
    L.rect(76, H - 2, 84, H - 2, '^')                  # ...spikes at the bottom
    L.rect(92, FLOOR, 118, FLOOR, '=')                 # top the speed back up
    L.rect(125, 26, 182, FLOOR - 1, '#')               # a step up...
    L.rect(145, 18, 170, 24, '#')                      # ...into tunnel 2 (crouch-jump straight into its mouth if you dare)
    L.rect(174, 25, 176, 25, '^')                      # spikes past the tunnel's end: stand and hop
    L.rect(180, 24, 180, 25, 'E')
    return L


def lockstep(p1=183 / 120, p2=25 / 120, p3=23 / 120, lasers=True):   # phases: a full-speed dev run meets every beam off
    L = Lvl(180, H, "Lockstep", "Buttons start the clock on their doors. Too far to run: fling, and mind the beams.")
    L.border('#')
    L.rect(0, FLOOR, 179, H - 1, '#')
    # 1. the launch plateau: a timed button, a pit, and a back wall high up
    L.rect(2, 10, 12, FLOOR - 1, '#')
    L.put(4, 9, 'S')
    L.obj("button b1 x=8 y=9 opens=d1 mode=timed time=2.5")
    L.rect(17, FLOOR - 1, 43, FLOOR - 1, '^')
    if lasers:
        L.obj(f"laser l1 x=28 y=2 dir=down period=2.0 on=1.0 phase={p1:.5f}")
    L.rect(44, 2, 45, FLOOR - 1, '#')                  # wall with a tall timed door
    L.obj("door d1 x=44 y=19 w=2 h=10")
    # 2. the foot race: button, two beams, a door
    L.obj("button b2 x=52 y=28 opens=d2 mode=timed time=4.5")
    if lasers:
        L.obj(f"laser l2 x=68 y=2 dir=down period=1.2 on=0.4 phase={p2:.5f}")
        L.obj(f"laser l3 x=84 y=2 dir=down period=1.2 on=0.4 phase={p3:.5f}")
    L.rect(102, 2, 103, FLOOR - 1, '#')
    L.obj("door d2 x=102 y=24 w=2 h=5")
    # 3. home stretch
    L.rect(111, FLOOR - 1, 115, FLOOR - 1, '^')
    L.rect(128, 27, 128, 28, 'E')
    return L


def ricochet(t1_phase=20 / 120):   # bullets miss every landing of a full-speed dev run
    L = Lvl(170, H, "Ricochet", "Bounce over the fire. Bullets fly through grills, and through your portals too.")
    L.border('#')
    L.rect(0, FLOOR, 169, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # 1. the bounce corridor: bullets skim the floor; jump as you land to bounce higher, then over the wall
    L.rect(15, FLOOR, 80, FLOOR, '%')
    L.rect(82, 20, 83, FLOOR - 1, '#')
    L.put(82, 28, '.')
    L.obj(f"turret t1 x=82 y=28 dir=left period=0.8 speed=16 phase={t1_phase:.5f}")
    L.rect(84, 20, 167, FLOOR - 1, '#')                # the ledge beyond
    # 2. a turret fires across the ledge above head height into a pillar; a caged button waits beyond
    L.rect(119, 2, 120, 15, '#')
    L.obj("turret t2 x=119 y=16 dir=left period=0.5 speed=18")
    L.rect(88, 2, 89, 17, '#')                         # pillar from the ceiling: the stream hits its right face
    L.rect(113, 10, 117, 12, ':')                      # grill cage: bullets pass, portal shots don't
    L.rect(114, 13, 116, 13, '#')
    L.rect(115, 11, 115, 12, '.')
    L.obj("button b1 x=115 y=12 opens=d1 mode=latch")
    L.rect(125, 2, 126, 19, '#')
    L.obj("door d1 x=125 y=14 w=2 h=6")
    L.rect(150, 18, 150, 19, 'E')
    return L


def overdrive():
    L = Lvl(190, H, "Overdrive", "Run into a wall portal, come up out of the floor. Speed becomes height, and back.")
    L.border('#')
    L.rect(0, FLOOR, 189, H - 1, '#')
    L.put(5, FLOOR - 1, 'S')
    # tier 0 -> 1: gel runway, a strip of panel floor, the first step
    L.rect(2, FLOOR, 62, FLOOR, '=')
    L.rect(70, 24, 185, FLOOR - 1, '#')
    # tier 1 -> 2 (every step is 5 tall: gel speed carried through a slide lifts you ~5.6)
    L.rect(72, 24, 108, 24, '=')
    L.rect(115, 19, 185, 23, '#')
    # tier 2 -> 3
    L.rect(117, 19, 150, 19, '=')
    L.rect(157, 14, 185, 18, '#')
    # tier 3: a shaft to the floor, and the far wall to come back out of, high up
    L.rect(171, 14, 174, FLOOR - 1, '.')
    L.rect(150, 6, 177, 7, '#')                        # the exit ledge up by the ceiling
    L.rect(152, 4, 152, 5, 'E')
    return L


LEVELS = [("11-relay", relay), ("12-slipstream", slipstream), ("13-lockstep", lockstep), ("14-ricochet", ricochet), ("15-overdrive", overdrive)]
