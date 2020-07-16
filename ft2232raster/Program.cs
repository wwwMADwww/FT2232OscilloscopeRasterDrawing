using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using FTD2XX_NET;

namespace ft2232raster
{

    class Program
    {
        protected static int _bufferSize = 1024 * 4;

        private static FTDI _channel;
        private static byte[] _buf;

        // 2000000 max
        static uint _baudrate = 2000000;


        private static byte[] _image;
        static int _resX = 480;
        static int _resY = 384;
        static int _resZ = 16;

        static int _porchBackX = 10;
        static int _porchFrontX = 10;

        static int _porchBackY = 10;
        static int _porchFrontY = 10;
        const byte _pinClockMask = 0b00100000;
        const byte _pinZMask     = 0b00001111;

        // const byte _pinClockX = 4;
        // const byte _pinClockY = 5;
        // const byte _pinClearX = 6;
        // const byte _pinClearY = 7;

        const byte _pinClockY = 6;
        const byte _pinClearY = 7;

        // const string _imagepath = @"E:\GFX\ILDA\mayoi420\Mayoi420 1.bmp";
        // const string _imagepath = @"E:\GFX\ILDA\dich\dich04.bmp";
        // const string _imagepath = @"E:\GFX\ILDA\chess 2.bmp";
        // const string _imagepath = @"E:\GFX\ILDA\chess.bmp";
        const string _imagepath = @"E:\GFX\ILDA\lines.bmp";

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");



            _channel = OpenChannel("A", _baudrate);

            _buf = new byte[_bufferSize];
            int pos = 0;

            Bitmap img = new Bitmap(_imagepath);

            _resX = img.Width;
            _resY = img.Height;

            _image = new byte[img.Width * img.Height];

            var colorOffsetKoeff = 2;
            var colorOffset = 256 - (256 / colorOffsetKoeff);

            for (int i = 0; i < img.Height; i++)
            {
                for (int j = 0; j < img.Width; j++)
                {
                    Color pixel = img.GetPixel(j, i);

                    _image[j + (i * img.Width)] = (byte) ((((pixel.G / colorOffsetKoeff) + colorOffset) / _resZ) ^ _pinZMask);
                }
            }

            img.Dispose();

            DrawDirect();

        }


        static void DrawDirect()
        {
            int pos = 0;

            while (true)
            {

                foreach (var y in Enumerable.Range(-_porchFrontY, _porchFrontY + _resY + _porchBackY))
                {
                    foreach (var x in Enumerable.Range(-_porchFrontX, _porchFrontX + _resX + _porchBackX))
                    {

                        if (pos >= _bufferSize)
                        {
                            pos = 0;

                            WriteToChannel(_channel, _buf);
                        }

                        byte reg = _pinClockMask;

                        if (x >= _resX)
                        {
                            // reg ^= (1 << _pinClearX);

                            if (y >= _resY)
                            {
                                reg ^= (1 << _pinClearY);

                            }
                            else
                            {
                                reg ^= (1 << _pinClockY);
                            }

                        }

                        byte z = (byte)(_resZ - 1);
                        if ((0 <= x && x < _resX) && ((0 <= y) && (y < _resY)))
                            z = _image[x + ((_resY - y - 1) * _resX)];

                        reg |= (byte)(z & _pinZMask);


                        _buf[pos] = reg;

                        pos++;
                    }


                    // buf[pos] = (byte) (reg | (1 << _pinClockX));
                    // 
                    // pos++;

                }
            }
        }


        static void WriteToChannel(FTDI channel, byte[] dataBuf)
        {
            var writtenTotal = 0;

            byte[] sendbuf = dataBuf;

            while (writtenTotal < dataBuf.Length)
            {
                uint written = 0;
                FTDI.FT_STATUS status;

                status = channel.Write(sendbuf, dataBuf.Length - writtenTotal, ref written);

                if (status == FTDI.FT_STATUS.FT_IO_ERROR)
                {
                    Console.WriteLine($"Write status is {status}");
                    continue;
                }
                else
                if (status == FTDI.FT_STATUS.FT_OTHER_ERROR)
                {
                    Console.WriteLine($"Write status is {status}");

                    status = channel.Close();
                    Console.WriteLine($"Close status is {status}");
                    channel.Dispose();

                    channel = OpenChannel("A", _baudrate);
                    Console.WriteLine($"OpenChannel status is {status}");
                    Debug.Assert(status == FTDI.FT_STATUS.FT_OK);
                }
                else
                {
                    if (status != FTDI.FT_STATUS.FT_OK)
                    {
                        Console.WriteLine($"Write status is {status}");
                        Debug.Assert(status == FTDI.FT_STATUS.FT_OK);
                    }
                }

                writtenTotal += (int)written;

                sendbuf = new byte[dataBuf.Length];
                Array.Copy(dataBuf, writtenTotal, sendbuf, 0, dataBuf.Length - writtenTotal);
            }
        }


        static FTDI OpenChannel(string channelName, uint baudRate)
        {

            FTDI.FT_STATUS status = FTDI.FT_STATUS.FT_OTHER_ERROR;

            FTDI res;

            while (true)
            {
                status = FTDI.FT_STATUS.FT_OTHER_ERROR;

                // res = new FTDI("/usr/local/lib/libftd2xx.so");
                // res = new FTDI("FTD2XX");
                res = new FTDI();


                FTDI.FT_DEVICE_INFO_NODE[] devicelist = new FTDI.FT_DEVICE_INFO_NODE[255];

                status = res.GetDeviceList(devicelist);

                // RUN WITH SUDO!!!11!!1!
                Console.WriteLine($"getdevicelist status is {status}");
                foreach (var device in devicelist.Where(x => x != null))
                {
                    Console.WriteLine($"Description is '{device.Description}'");
                    Console.WriteLine($"SerialNumber is '{device.SerialNumber}'");
                    Console.WriteLine($"ID is '{device.ID}'");
                    Console.WriteLine($"LocId is '{device.LocId}'");
                    Console.WriteLine($"Type is '{device.Type}'");
                    Console.WriteLine($"------");
                }

                // status = res.OpenBySerialNumber(channelName);
                // status = res.OpenByLocation(12337);
                status = res.OpenBySerialNumber("A");

                Console.WriteLine($"OpenByLocation status is {status}");

                if (status != FTDI.FT_STATUS.FT_OK)
                {
                    Console.WriteLine("press enter to retry open");
                    Console.ReadLine();
                    res.Dispose();
                    continue;
                }

                break;

                // Debug.Assert(status == FTDI.FT_STATUS.FT_OK);
            }

            status = res.SetBaudRate(baudRate);
            Console.WriteLine($"SetBaudRate status is {status}");
            Debug.Assert(status == FTDI.FT_STATUS.FT_OK);

            status = res.SetLatency(0);
            Console.WriteLine($"SetLatency status is {status}");
            Debug.Assert(status == FTDI.FT_STATUS.FT_OK);

            // enable async bitbang mode for all 8 pins
            status = res.SetBitMode(0b11111111, FTDI.FT_BIT_MODES.FT_BIT_MODE_ASYNC_BITBANG);
            Console.WriteLine($"SetBitMode status is {status}");
            Debug.Assert(status == FTDI.FT_STATUS.FT_OK);

            status = res.SetTimeouts(1, 1);
            Console.WriteLine($"SetTimeouts status is {status}");
            Debug.Assert(status == FTDI.FT_STATUS.FT_OK);

            return res;

        }

    }
}
