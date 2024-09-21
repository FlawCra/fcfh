using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Bmp;
using System.IO;
using System.Collections.Generic;
using System;

namespace fcfh
{
    public static class ImageWriter
    {
        public const string MAGIC = "BMPENC";

        /// <summary>
        /// BMPENC file
        /// </summary>
        public struct ImageFile
        {
            /// <summary>
            /// Checks if this Instance is Empty or Valid
            /// </summary>
            public bool IsEmpty
            {
                get
                {
                    return string.IsNullOrEmpty(FileName) && (Data == null || Data.Length == 0);
                }
            }

            /// <summary>
            /// File Name
            /// </summary>
            public string FileName;
            /// <summary>
            /// File Data
            /// </summary>
            public byte[] Data;
        }

        /// <summary>
        /// Provides encoding and decoding from Image Headers
        /// </summary>
        public static class HeaderMode
        {
            /// <summary>
            /// Header name
            /// </summary>
            /// <remarks>Upper-and lowercase follow a format: http://www.libpng.org/pub/png/spec/1.2/PNG-Structure.html#Chunk-naming-conventions</remarks>
            public const string DEFAULT_HEADER = "fcFh";

            /// <summary>
            /// Reads a byte array as PNG
            /// </summary>
            /// <param name="Data">Image Bytes</param>
            /// <returns>Header list</returns>
            public static PNGHeader[] ReadPNG(byte[] Data)
            {
                using (var MS = new MemoryStream(Data, false))
                {
                    return ReadPNG(MS);
                }
            }

            /// <summary>
            /// Reads a stream as PNG
            /// </summary>
            /// <remarks>Stream must start with a PNG header at the current position, Stream is left open</remarks>
            /// <param name="S">Stream</param>
            /// <returns>Header list</returns>
            public static PNGHeader[] ReadPNG(Stream S)
            {
                var Headers = new List<PNGHeader>();

                byte[] HeaderData = new byte[8];
                S.Read(HeaderData, 0, 8);
                if (BitConverter.ToUInt64(HeaderData, 0) == PNGHeader.PNG_MAGIC)
                {
                    do
                    {
                        Headers.Add(new PNGHeader(S));
                    } while (Headers.Last().HeaderName != "IEND");
                }
                return Headers.ToArray();
            }

            /// <summary>
            /// Reads a file as PNG
            /// </summary>
            /// <param name="FileName">File name</param>
            /// <returns>Header list</returns>
            public static PNGHeader[] ReadPNG(string FileName)
            {
                using (var FS = File.OpenRead(FileName))
                {
                    return ReadPNG(FS);
                }
            }

            /// <summary>
            /// Writes a collection of PNG Headers to a byte array
            /// </summary>
            /// <remarks>This will check for IHDR and IEND positions</remarks>
            /// <param name="Headers">PNG Headers</param>
            /// <returns>PNG bytes</returns>
            public static byte[] WritePNG(IEnumerable<PNGHeader> Headers)
            {
                var Arr = Headers.ToArray();
                if (Arr.Length > 1 && Arr.First().HeaderName == "IHDR" && Arr.Last().HeaderName == "IEND")
                {
                    using (var MS = new MemoryStream())
                    {
                        MS.Write(BitConverter.GetBytes(PNGHeader.PNG_MAGIC), 0, 8);
                        foreach (var H in Headers)
                        {
                            H.WriteHeader(MS);
                        }
                        return MS.ToArray();
                    }
                }
                return null;
            }

            /// <summary>
            /// Checks if the given File is a PNG
            /// </summary>
            /// <remarks>Only checks the 8 byte header</remarks>
            /// <param name="FileName">File name</param>
            /// <returns>True if PNG, false otherwise or on I/O error</returns>
            public static bool IsPNG(string FileName)
            {
                try
                {
                    using (var FS = File.OpenRead(FileName))
                    {
                        using (var BR = new BinaryReader(FS))
                        {
                            return BR.ReadUInt64() == PNGHeader.PNG_MAGIC;
                        }
                    }
                }
                catch
                {
                }
                return false;
            }

            /// <summary>
            /// Stores data in a PNG header
            /// </summary>
            /// <param name="FullFileName">Source file</param>
            /// <param name="ImageFile">Existing Image file</param>
            /// <param name="HeaderName">Name of Header</param>
            /// <returns>PNG with custom header</returns>
            /// <remarks>This process is repeatable</remarks>
            public static byte[] CreateImageFromFile(string FullFileName, string ImageFile, string HeaderName = DEFAULT_HEADER)
            {
                using (var FS = File.OpenRead(FullFileName))
                {
                    using (var IMG = File.OpenRead(ImageFile))
                    {
                        return CreateImageFromFile(FS, Path.GetFileName(FullFileName), IMG, HeaderName);
                    }
                }
            }

            /// <summary>
            /// Stores data in a PNG header
            /// </summary>
            /// <param name="InputFile">Source File Stream</param>
            /// <param name="FileName">Source File Name (no path)</param>
            /// <param name="InputImage">Source Image Stream</param>
            /// <param name="HeaderName">Name of Header</param>
            /// <returns>PNG with custom header</returns>
            /// <remarks>This process is repeatable</remarks>
            public static byte[] CreateImageFromFile(Stream InputFile, string FileName, Stream InputImage, string HeaderName = DEFAULT_HEADER)
            {
                var Headers = ReadPNG(InputImage).ToList();
                if (Headers.Count > 0)
                {
                    var Data = Tools.ReadAll(InputFile);
                    Headers.Insert(1, new PNGHeader(HeaderName,
                        Encoding.UTF8.GetBytes(MAGIC)
                        .Concat(BitConverter.GetBytes(Tools.IntToNetwork(Encoding.UTF8.GetByteCount(FileName))))
                        .Concat(Encoding.UTF8.GetBytes(FileName))
                        .Concat(BitConverter.GetBytes(Tools.IntToNetwork(Data.Length)))
                        .Concat(Data)
                        .ToArray()));
                    return WritePNG(Headers);
                }
                return null;
            }
        }

        /// <summary>
        /// Provides encoding and decoding from Pixel Data
        /// </summary>
        public static class PixelMode
        {
            /// <summary>
            /// Bytes for each Pixel. In 24bpp Mode this is 24/8=3
            /// </summary>
            private const int BYTES_PER_PIXEL = 24 / 8;

            /// <summary>
            /// Saves binary Data as an Image
            /// </summary>
            /// <param name="FullFileName">Source File Name to process</param>
            /// <param name="PNG">Use PNG instead of BMP</param>
            /// <param name="AllowDirectDecode">if true, Data is stored so it appears in Order when viewed as BMP</param>
            /// <returns>Image Data</returns>
            public static byte[] CreateImageFromFile(string FullFileName, bool PNG = false, bool AllowDirectDecode = false)
            {
                using (var FS = File.OpenRead(FullFileName))
                {
                    return CreateImageFromFile(FS, Path.GetFileName(FullFileName), PNG, AllowDirectDecode);
                }
            }

            /// <summary>
            /// Saves binary Data as an Image
            /// </summary>
            /// <param name="Input">Source Content</param>
            /// <param name="FileName">File Name to store</param>
            /// <param name="PNG">Use PNG instead of BMP</param>
            /// <param name="AllowDirectDecode">if true, Data is stored so it appears in Order when viewed as BMP</param>
            /// <returns>Image Data</returns>
            public static byte[] CreateImageFromFile(Stream Input, string FileName, bool PNG = false, bool AllowDirectDecode = false)
            {
                if (Input == null)
                {
                    throw new ArgumentNullException(nameof(Input));
                }
                byte[] AllData = Tools.ReadAll(Input);
                byte[] Data =
                    // Header
                    Encoding.UTF8.GetBytes(MAGIC)
                    // File name length (big-endian)
                    .Concat(BitConverter.GetBytes(Tools.IntToNetwork(Encoding.UTF8.GetByteCount(FileName))))
                    // File name
                    .Concat(Encoding.UTF8.GetBytes(FileName))
                    // Data length (big-endian)
                    .Concat(BitConverter.GetBytes(Tools.IntToNetwork(AllData.Length)))
                    // Data
                    .Concat(AllData)
                    // Make array
                    .ToArray();

                var W = (int)Math.Sqrt(Data.Length / BYTES_PER_PIXEL);
                // Width must be a multiple of 4
                W -= W % 4;
                var H = (int)Math.Ceiling(Data.Length / (double)BYTES_PER_PIXEL / W);

                using (var image = new SixLabors.ImageSharp.Image<Rgb24>(W, H))
                {
                    byte[] DataArray = Data; // Ensure consistent naming

                    int dataLength = DataArray.Length;
                    int maxDataLength = W * H * BYTES_PER_PIXEL;

                    // Ensure we don't exceed the image capacity
                    if (dataLength > maxDataLength)
                    {
                        throw new ArgumentException("Data is too large to fit in the generated image.");
                    }

                    // Write pixel data using ProcessPixelRows
                    image.ProcessPixelRows(accessor =>
                    {
                        int dataIndex = 0;

                        for (int y = 0; y < accessor.Height; y++)
                        {
                            Span<Rgb24> pixelRow = accessor.GetRowSpan(y);

                            for (int x = 0; x < accessor.Width; x++)
                            {
                                if (dataIndex + 2 >= DataArray.Length)
                                    break; // Prevent overflow if Data.Length exceeds pixel capacity

                                byte r = DataArray[dataIndex++];
                                byte g = (dataIndex < DataArray.Length) ? DataArray[dataIndex++] : (byte)0;
                                byte b = (dataIndex < DataArray.Length) ? DataArray[dataIndex++] : (byte)0;

                                pixelRow[x] = new Rgb24(r, g, b);
                            }

                            if (dataIndex >= DataArray.Length)
                                break;
                        }
                    });

                    using (var ms = new MemoryStream())
                    {
                        if (PNG)
                        {
                            image.Save(ms, new PngEncoder());
                        }
                        else
                        {
                            image.Save(ms, new BmpEncoder());
                        }
                        return ms.ToArray();
                    }
                }
            }

            /// <summary>
            /// Extracts a File From an Image
            /// </summary>
            /// <param name="Input"></param>
            /// <returns></returns>
            public static ImageFile CreateFileFromImage(Stream Input, bool AllowDirectDecode = false)
            {
                using (var image = Image.Load<Rgb24>(Input))
                {
                    // Calculate the total number of bytes to copy
                    int totalBytes = image.Width * image.Height * BYTES_PER_PIXEL; // 3 bytes per pixel
                    byte[] Data = new byte[totalBytes];

                    // Copy pixel data into the byte array
                    image.CopyPixelDataTo(Data);

                    // Check MAGIC
                    string magic = Encoding.UTF8.GetString(Data, 0, MAGIC.Length);
                    if (magic != MAGIC)
                    {
                        if (!AllowDirectDecode)
                        {
                            // Data might be in reverse order if BMP was used
                            Array.Reverse(Data);
                            magic = Encoding.UTF8.GetString(Data, 0, MAGIC.Length);

                            if (magic != MAGIC)
                            {
                                return new ImageFile(); // Returns default with nulls
                            }
                        }
                        else
                        {
                            return new ImageFile(); // Returns default with nulls
                        }
                    }

                    // Parse the encoded data
                    try
                    {
                        int offset = MAGIC.Length;
                        int fileNameLength = Tools.IntToHost(BitConverter.ToInt32(Data, offset));
                        offset += 4;

                        string FileName = Encoding.UTF8.GetString(Data, offset, fileNameLength);
                        offset += fileNameLength;

                        int dataLength = Tools.IntToHost(BitConverter.ToInt32(Data, offset));
                        offset += 4;

                        byte[] fileData = new byte[dataLength];
                        Array.Copy(Data, offset, fileData, 0, dataLength);

                        return new ImageFile
                        {
                            FileName = FileName,
                            Data = fileData
                        };
                    }
                    catch
                    {
                        return new ImageFile(); // Returns default with nulls
                    }
                }
            }
        }
        
    }
}
