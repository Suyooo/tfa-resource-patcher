using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace ResourcePatcher
{
    public class TextureNode
    {
        public Rectangle r;
        public TextureNode(int x, int y, int w, int h)
        {
            r = new Rectangle(x, y, w, h);
        }
        public TextureNode(Rectangle r2)
        {
            r = r2;
        }
        public bool overlap(Rectangle other)
        {
            return r.IntersectsWith(other);
        }
    }

    public class TextureAtlas
    {
        public List<TextureNode> nodes;
        public int width;
        public int height;
        public TextureAtlas(int w, int h)
        {
            nodes = new List<TextureNode>();
            width = w;
            height = h;
        }
        public void addExistingTexture(int x, int y, int w, int h)
        {
            nodes.Add(new TextureNode(x, y, w, h));
        }

        public TextureNode findSpaceForTexture(int w, int h)
        {
            var checkInterval = 10;
            var newTexture = new Rectangle(0, 0, w, h);
            while (newTexture.Bottom < height)
            {
                while (newTexture.Right < width)
                {
                    var isFree = true;
                    foreach (TextureNode n in nodes)
                    {
                        if (n.overlap(newTexture))
                        {
                            isFree = false;
                            break;
                        }
                    }
                    if (isFree)
                    {
                        var tn = new TextureNode(newTexture);
                        nodes.Add(tn);
                        return tn;
                    }
                    newTexture.Offset(checkInterval, 0);
                }
                newTexture.X = 0;
                newTexture.Offset(0, checkInterval);
            }
            return null;
        }
    }
}
