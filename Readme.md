A dynamic proxy implementation, just for fun.

Saturday-night project. 
Original intent was to see if it is possible to write a dynamic proxy implementation with less overhead
than Castle.DynamicProxy for use in a hot spot in code.

Result: 400 lines of code.
Somewhat faster in certain benchmarks than Castle.

Not tested in production.
Very little argument validation logic.
Use at your own risk, or better don't.
 
### Benchmarks ###
P95 of call duration, benchmarks with Benchmarks.DotNet.
CreateProxyForTarget

|| Simple | Castle | By Hand |
| ---| --- | --- | --- |
|No parameters |85.8477 ns | 120.3021 ns | 6.8385 ns|
|Multiple value type parameters |190.4640 ns  | 209.0111 ns  | 5.7563 ns|
Multiple reference parameters |126.6326 ns  | 187.8527 ns   | 5.8213 ns |

CreateProxyWithoutTarget

|| Simple | Castle | By Hand |
| ---| --- | --- | --- |
|No parameters |37.8898 ns | 84.0170 ns | 2.7652 ns|
|Multiple value type parameters |131.4094 ns  | 155.3810 ns  | 3.6198 ns|
|Multiple reference parameters|89.6910 ns  | 106.0226 ns    | 3.4135 ns  |

