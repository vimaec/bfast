# BFAST

BFAST stands for the **B**inary **F**ormat for **A**rray **S**erialization and **T**ransmission. 
It is a specification for encoding collections of binary arrays of data that can efficiently serialized/deserialized 
between data structures and disk, or over a network and from different languages and systems. 

The BFAST reference implementation in C++ is under 300 lines of code including documentation and spacing requiring only
the STL. BFAST is also used as the underlying binary layout format for the [G3D geomtry file format](https://github.com/ara3d/g3d).

## Use Case

You would use the BFAST structure if you have a binary data to serialize that is mostly in the form of long 
arrays. For example geometry data, meshes, or a set of files that you want to bundle together without necessarily
compressing them or trying to re-implement TAR. 

## Features

* Extremely small and easy to implement conformant encoders and decoders in different languages 
* Format and endianess easily identified through a magic number at the front of the file
* Data arrays are 32 byte aligned - facilitates casting to SIMD data types
* Array offsets are encoded using 64-bits so it supports large sets of data.

## Specification

The file format consists of three sections:

* header - Fixed size descriptor (32 bytes + 32 bytes padding) describing the file contents   
* ranges - An array of offset pairs indicating where each array starts and finishes relative to the file
* data  - Raw data containing  

The header has the following layout:  

```
    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 32)]
    public struct FileHeader
    {
        [FieldOffset(0)]    public long Magic;         // 0xBFA5
        [FieldOffset(8)]    public long DataStart;     // <= file size
        [FieldOffset(16)]   public long DataEnd;       // >= DataStart and <= file size
        [FieldOffset(24)]   public long NumArrays;     // number of arrays 
    }
```

The ranges start at byte 64. There are `NumArrays` of them and they have has the following format. The `Begin` and `End` values are byte offsets relative to the beginning of the file:

```
    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16)]
    public struct Range
    {
        [FieldOffset(0)] public long Begin;
        [FieldOffset(8)] public long End;
    }		
```

The data section starts at the first 64 byte aligned address immediately following the last `Range` value.

## Why is BFAST so Fast? 

Many file or data formats require processing of the data once it comes in. Because the BFAST format is specifically for encoding binary arrays on contiguous data, 
it allows the consumer (decoder) of the data to allocate a single block of memory, read all of the data at once using a system call, and then set up array views, 
to point to the already allocated memory. For example, in C++ this can be done using begin/end iterators, ranges, spans, etc. and in JavaScript it can be done 
using ArrayBuffers and DataViews. 

## How to Extend BFAST?

BFAST by itself, is a minimal specification for efficiently encoding N byte-arrays. By convention the first array is usally a UTF-8 encoded JSON string which 
different file formats can use to encode additional information about the file.