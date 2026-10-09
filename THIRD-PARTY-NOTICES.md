# Third-party notices

## Nefarius.ViGEm.Client 1.21.256 (merged into BodycamFpvFix.exe)

Source: https://github.com/nefarius/ViGEm.NET · Package: https://www.nuget.org/packages/Nefarius.ViGEm.Client/1.21.256

```
MIT License

Copyright (c) 2018 Benjamin Höglinger-Stelzer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## ViGEmBus 1.22.0 (not included; downloaded on request)

The program can download and start the official, signed ViGEmBus installer from
https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0 (BSD 3-Clause License,
Copyright Nefarius Software Solutions e.U.). It checks the file's SHA-256 before running it.

## ILRepack 2.0.48 (build tool only, not included)

`build.ps1` uses ILRepack (https://github.com/gluck/il-repack, Apache License 2.0) to merge the
ViGEm client library into the exe. No ILRepack code ends up in BodycamFpvFix.exe.
