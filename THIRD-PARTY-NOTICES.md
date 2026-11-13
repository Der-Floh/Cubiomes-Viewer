# Third-party notices

Cubiomes Viewer includes or links the following third-party software.

## cubiomes

Source: https://github.com/Cubitect/cubiomes (git submodule `native/cubiomes`). The C sources are compiled into the native `cvnative` library, its Beta 1.7 terrain noise sampler is copied into `native/shim/cv_beta_noise.inc` with its permutation table indexed as the game indexes it, and parts of its 48-bit seed arithmetic are re-implemented in C# in `Cubiomes_Viewer.Engine`.

```text
MIT License

Copyright (c) 2020 Cubitect

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

## Material Symbols

Source: https://fonts.google.com/icons. The icons of the buttons in `Cubiomes_Viewer/Assets/Icons` are Material Symbols in their outlined and filled forms; their fill is changed to `currentColor` so that the app can colour them. The glyphs in `Assets/Icons/Markers` and `Assets/Icons/Dimensions` are drawn for this project.

```text
Copyright Google LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    https://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```
