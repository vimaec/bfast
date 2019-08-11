/*
    BFAST - Binary Format for Array Streaming and Transmission
    Copyright 2019, VIMaec LLC
    Copyright 2018, Ara 3D, Inc.
    Usage licensed under terms of MIT License
	https://github.com/vimaec/bfast
*/

function parseBFast( arrayBuffer )
{
    // Cast the input data to 32-bit integers 
    // Note that according to the spec they are 64 bit numbers. In JavaScript you can't have 64 bit integers, 
    // and it would bust the amount of memory we can work with in most browsers and low-power devices  
    var data = new Int32Array(arrayBuffer);

    // Parse the header
    var header = {
        Magic:      data[0], // The Magic number
        DataStart:  data[2], // <= file size and >= ArrayRangesEnd and >= FileHeader.ByteCount
        DataEnd:    data[4], // >= DataStart and <= file size
        NumArrays:  data[6], // number of arrays 
    }

    // Check validity of data 
    if (header.Magic != 0xBFA5) throw new Error("Not a BFAST file");
    if (data[1] != 0) throw new Error("Expected 0 in byte position 0");
    if (data[3] != 0) throw new Error("Expected 0 in byte position 8");
    if (data[5] != 0) throw new Error("Expected 0 in position 16");
    if (data[7] != 0) throw new Error("Expected 0 in position 24");
    if (header.DataStart <= 64 || header.DataStart >= arrayBuffer.length) throw new Error("Data start is out of valid range");
    if (header.DataEnd < header.DataStart || header.DataEnd >= arrayBuffer.length) throw new Error("Data end is out of vaid range");
    if (header.NumArrays < 0 || header.NumArrays > header.DataEnd) throw new Error("Number of arrays is invalid");
            
    // Compute each buffer
    var buffers = [];
    var pos = 8; 
    for (var i=0; i < header.NumArrays; ++i) {
        var begin = data[pos+0];
        var end = data[pos+2];            

        // Check validity of data 
        if (data[pos+1] != 0) throw new Error("Expected 0 in position " + (pos + 1) * 4);
        if (data[pos + 3] != 0) throw new Error("Expected 0 in position " + (pos + 3) * 4);
        if (begin < header.DataStart || begin > header.DataEnd) throw new Error("Buffer start is out of range");
        if (end < begin || end > header.DataEnd) throw new Error("Buffer end is out of range");
        if (begin % 64 != 0) throw new Error("Beginning of data is not aligned on 64 byte boundaries");

        pos += 4;      
        var buffer = new Uint8Array(arrayBuffer, begin, end - begin);
        rawBuffers.push(buffer);
    }

    if (rawBuffers.length < 1) throw new Error("Expected at least one buffer that contains names");
    var names = new TextDecoder("utf-8").decode(buffers[0]).split('\0');
    if (names.length != buffers.length - 1) throw new Error("Expected number of names to match number of buffers");

    // Return the bfast structure 
    return {
        header: header,
        names: names,
        buffers: buffers.slice(1),
    }
};
