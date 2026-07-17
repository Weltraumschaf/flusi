# FluSi

This is a simple flight simulator for my six years old son.

## Controls

Keyboard only. Click inside the Game view first, otherwise the keys go to the
Editor instead of the plane.

| Key         | Action                                          |
| ----------- | ----------------------------------------------- |
| Up arrow    | Nose down                                       |
| Down arrow  | Nose up                                         |
| Left arrow  | Bank and turn left                              |
| Right arrow | Bank and turn right                             |
| `W`         | Faster                                          |
| `S`         | Slower                                          |
| `C`         | Switch between cockpit and external orbit view  |
| `A` / `D`   | Rotate the orbit camera (external view only)    |
| `L`         | Toggle the auto-level assist (starts on)        |
| `G`         | Raise / lower the landing gear                  |

**The pitch keys are inverted on purpose, not by mistake.** The up arrow is the
stick pushed forward, which puts the nose down; the down arrow is the stick
pulled back, which brings the nose up. That is how a real aeroplane works and
how the pilot this was built for already flies. Please do not "correct" it.

Turning is coordinated: the left and right arrows roll the plane into the turn
rather than yawing it flat. With the auto-level assist on, releasing the arrows
brings the wings and nose back to level; with it off, the plane holds whatever
bank and pitch you leave it in.

## Technoligy Stack

- Unity with C#.
- Running on macOS and Linux.

### Setup Tools

```shell
brew install --cask unity-hub
brew install --cask dotnet-sdk
```