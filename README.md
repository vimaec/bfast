# BFAST

BFAST stands for the **B**inary **F**ormat for **A**rray **S**erialization and **T**ransmission. It is a specification for encoding binary arrays of data with JSON data that can efficiently serialized/deserialized between data structures and disk, or over a network.

## Features

* Extremely small and easy to implement conformant encoders and decoders in different languages 
* Format easily identified through a magic number at the front of the file
* Endianess of encoding easily detected 
* Data arrays are 64 byte aligned - facilitates casting to SIMD data types
* Array offsets are encoded using 64-bits, allowing files to be 
* Variable width meta-information section for embedding JSON data

## Specification

The C++ header says it all: 

```
	struct array_offset {
		uint64_t begin;
		uint64_t end;
	};
	
	struct alignas(8) file_header {
		uint64_t magic;			  // Either 0xBFA5 (same-endian) of 0x5AFB0000 (different-endian)
		uint64_t meta_start;	// >= sizeof(header) and >= 64 and <= file_size
		uint64_t meta_end;		// >= desc_start and <= data_start and <= file_size  
		uint64_t data_start;	// >= meta_end and modulo 64 == 0 and <= file_size
		uint64_t data_end;		// >= data_start and <= file_size
		uint64_t num_arrays;	// number of arrays
		array_offset[1];		  // an array of array offset pairs, the actual size of the array can vary 
	};

	const MAGIC = 0xBFA5;
	const SWAPPED_MAGIC = 0x5AFB0000;
```

## Why is BFAST so Fast? 

Many file or data formats require multiple memory allocations, and then processing of the data once it comes in. Because the BFAST format is specifically for encoding binary arrays on contiguous data, it allows the consumer (decoder) of the data to allocate a single block of memory, read all of the data at once using a system call, and then set up array views, to point to the already allocated memory. For example, in C++ this can be done using begin/end iterators, ranges, spans, etc. and in JavaScript it can be done using ArrayBuffers and DataViews. 

## How to Extend BFAST?

BFAST by itself, is a very bare-bones specification for raw data but the meta-information section is designed to contain a variable width JSON string which different file types can use to encode additional information about the file, or about the different arrays in the data section. One example usage of BFAST is the g3d geometry file format. 
