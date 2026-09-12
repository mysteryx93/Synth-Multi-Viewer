import vapoursynth as vs
core = vs.core
clip = core.std.BlankClip(width=640, height=480, length=240, fpsnum=24, fpsden=1, format=vs.RGB24, color=[32, 96, 192])
clip.set_output()
