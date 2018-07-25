# TASF - Typed Array Serialization Format

TASF stands for the typed array serialization format. It is a specification for encoding collections of JavaScrupt typed arrays that can be efficiently serialized and deserialized. 

4 bytes - magic number, ASCII encoding of the letters 'T', 'A", 'S', 'F'. 
4 bytes - data start (B), byte offset from beginning of file
M bytes - begin of meta-information, a string containing JSON information   
N bytes - data block. 

## Meta-Information

The meta-information string has the following parts:

```
{
  version: string, // "1.0"
  arrays: [],
}
```

The verion string should be "1.0". 
The arrays field is an array of JSON objects with the following structure:

```
{
  type: number, // 0..8
  start: number, // byte-offset from beginning of data-block where the data array starts 
  length: number, // length of data-block 
  name: string, // optional
}
```

## Extracting Arrays from the TSAF 

TODO
