# BFAST

BFAST stands for the **B**inary **F**ormat for **A**rray **S**erialization and **T**ransmission. 

BFAST is a data format for simple, efficient, and reliable serialization and deserialization of 
collections of binary data with optional names as a single block of data. It is designed so
that efficient and correct serializers can be quickly written in different languages. 

## Use Case

You would use the BFAST structure if you have a binary data to serialize that is mostly in the form of 
long arrays. For example a set of files that you want to bundle together without wanting to bring in 
the overhead of a compression library or re-implementing TAR.We use BFAST to encode mesh data and as 
containers for other data. 

## Features

* Very small implementation overhead 
* Easy to implement efficient and conformant encoders and decoders in different languages 
* Format and endianess easily identified through a magic number at the front of the file
* Data arrays are 32 byte aligned - facilitates casting to SIMD data types (eg. SSE2 types)
* Array offsets are encoded using 64-bits to supports large data sets
* Positions of data buffers are encoded in the beginning of the file

# Specification

The file format consists of three sections:

* Header - Fixed size descriptor (32 byte) describing the file contents   
* Ranges - An array of offset pairs indicating where each data buffers starts and finishes
* Data   - 32-byte aligned concatenated data buffers. Each data buffer is 32-byte aligned. 

The header has the following layout:  

```
    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 32)]
    public struct Header
    {
        [FieldOffset(0)]    public long Magic;         // 0xBFA5
        [FieldOffset(8)]    public long DataStart;     // <= File size and >= 32 + Sizeof(Range) * NumArrays 
        [FieldOffset(16)]   public long DataEnd;       // >= DataStart and <= file size
        [FieldOffset(24)]   public long NumArrays;     // Number of arrays
    }
```

The ranges start at byte 32. There are `NumArrays` of them and they have has the following format. 
The `Begin` and `End` values are byte offsets relative to the beginning of the file:

```
    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16)]
    public struct Range
    {
        [FieldOffset(0)] public long Begin;
        [FieldOffset(8)] public long End;
    }		
```

The data section (the `DataStart` field of the Header, starts at the first 32 byte 
aligned address immediately following the last `Range` value.

The first data buffer contain the names of the subsequent buffers as a concatenated list of Utf-8 encoded 
strings separated by null characters.

## Implementations 

There is a C# 

## Rationale

