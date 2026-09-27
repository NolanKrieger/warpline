"""Tiny level-building helper: draw rectangles of tiles, write the ASCII .lvl the game reads.
Coordinates are tiles, x right, y down, rectangles inclusive. Run: python3 build.py"""

class Lvl:
    def __init__(self, w, h, name, hint=""):
        self.w, self.h, self.name, self.hint = w, h, name, hint
        self.g = [['.'] * w for _ in range(h)]
        self.objects = []

    def rect(self, x0, y0, x1, y1, ch):
        for y in range(max(0, y0), min(self.h, y1 + 1)):
            for x in range(max(0, x0), min(self.w, x1 + 1)):
                self.g[y][x] = ch
        return self

    def border(self, ch='#', floor=True):
        self.rect(0, 0, self.w - 1, 1, ch)             # ceiling (2 thick)
        self.rect(0, 0, 1, self.h - 1, ch)             # left wall
        self.rect(self.w - 2, 0, self.w - 1, self.h - 1, ch)
        if floor:
            self.rect(0, self.h - 2, self.w - 1, self.h - 1, ch)
        return self

    def put(self, x, y, ch):
        self.g[y][x] = ch
        return self

    def obj(self, line):
        """A machine line: door/button/laser/receiver/turret (see src/Sim/Machines.cs)."""
        self.objects.append(line)
        return self

    def text(self):
        out = [f"name: {self.name}"]
        if self.hint:
            out.append(f"hint: {self.hint}")
        out.append("---")
        out += [''.join(r) for r in self.g]
        if self.objects:
            out.append("---")
            out += self.objects
        return '\n'.join(out) + '\n'

    def save(self, path):
        with open(path, 'w') as f:
            f.write(self.text())
