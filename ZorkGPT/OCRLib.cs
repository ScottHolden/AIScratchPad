using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZorkGPT
{
    internal class OCRLib
    {
        private readonly Dictionary<string, char> _lookup;
        public OCRLib(Dictionary<string, char> lookup) 
        {
            _lookup = lookup;
        }
        public OCRLib() : this(new Dictionary<string, char>()) { }

        public static OCRLib Load(string path)
            => new(JsonSerializer.Deserialize<Dictionary<string, char>>(File.ReadAllText(path)) ?? throw new Exception("Unable to load OCRLib"));
        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(_lookup));

        public char Get(string key)
            => _lookup.TryGetValue(key, out char value) ? value : '?';
        public char Get(byte[] key)
            => Get(Convert.ToBase64String(key));
        public void Add(string key, char value)
            => _lookup[key] = value;
        public void Add(byte[] key, char value)
            => Add(Convert.ToBase64String(key), value);

        public string[] ReadScreen(Bitmap bmp)
        {
            string[] result = new string[25];
            int cols = 80;
            int rows = 25;
            int w = 8;
            int h = 16;
            for (int row = 0; row < rows; row++)
            {
                result[row] = "";
                for (int col = 0; col < cols; col++)
                {
                    byte[] bChar = new byte[h];
                    for (int y = 0; y < h; y++)
                    {
                        byte b = 0;
                        for (int x = 0; x < w; x++)
                        {
                            if (bmp.GetPixel(col * w + x, row * h + y).B > 100)
                            {
                                b += (byte)(1 << x);
                            }
                        }
                        bChar[y] = b;
                    }
                    result[row] += Get(bChar);
                }
            }
            return result;
        }
    }
}
