/* ********************************************
 * TOWERFALL ASCENSION RESOURCE PATCHER
 *   Based on the Bartizan/TowerClimb project
 *   by derKha: https://github.com/Kha/Bartizan
 * *******************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Xml.Linq;

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

    public class ResourcePatcher
	{
		/// <summary>
		/// Insert new sprites into Atlas.
		/// </summary>
		public static void PatchSprites()
		{
            Console.Clear();
            DrawTitle();
			foreach (var patchFile in Directory.EnumerateDirectories(Path.Combine("patchfiles", "sprites"))) {
                var atlasName = new DirectoryInfo(patchFile).Name;
                Console.WriteLine("Patching atlas \"" + atlasName + "\"...");

                var originalFile = Path.Combine("Content", "Atlas", atlasName);
                File.Copy(originalFile + ".png", originalFile + "_o.png", true);
                File.Copy(originalFile + ".xml", originalFile + "_o.xml", true);
                Console.WriteLine("- Backups created.");

                var xml = XElement.Load(originalFile + "_o.xml");
                string[] files = Directory.GetFiles(patchFile, "*.png", SearchOption.AllDirectories);

                var spritesPatched = 0;
                var spritesTotal = 0;

                using (var baseImage = Bitmap.FromFile(originalFile + "_o.png"))
                {
                    Console.WriteLine("- Creating virtual atlas...");
                    var atlas = new TextureAtlas(baseImage.Width, baseImage.Height);
                    IEnumerable<XElement> textures =
                                    from el in xml.Elements("SubTexture")
                                    select el;
                    foreach (XElement e in textures)
                    {
                        atlas.addExistingTexture((int)e.Attribute("x"), (int)e.Attribute("y"), (int)e.Attribute("width"), (int)e.Attribute("height"));
                    }

					using (var g = Graphics.FromImage(baseImage))
						foreach (string file in files)
							using (var image = Bitmap.FromFile(file)) {
                                spritesTotal++;

                                string name = file.Substring(patchFile.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                                name = name.Substring(0, name.Length - ".png".Length);
                                Console.WriteLine("- Adding new sprite \""+name+"\"...");

                                IEnumerable<XElement> exists =
                                    from el in xml.Elements("SubTexture")
                                    where (string)el.Attribute("name") == name
                                    select el;
                                if (exists.Count() > 0)
                                {
                                    Console.WriteLine("- - This sprite already exists in this atlas.");
                                    var e = exists.First<XElement>();
                                    if ((int)(e.Attribute("width")) == image.Width && (int)(e.Attribute("height")) == image.Height)
                                    {
                                        Console.WriteLine("- - Same size, replacing sprite...");
                                        g.SetClip(new Rectangle((int)(e.Attribute("x")), (int)(e.Attribute("y")), (int)(e.Attribute("width")), (int)(e.Attribute("height"))));
                                        g.Clear(Color.Transparent);
                                        g.ResetClip();
                                        g.DrawImage(image, (int)(e.Attribute("x")), (int)(e.Attribute("y")));
                                        Console.WriteLine("- - Replaced at (" + (int)(e.Attribute("x")) + ", " + (int)(e.Attribute("y")) + ").");
                                        spritesPatched++;
                                    }
                                    else
                                    {
                                        Console.WriteLine("- - Different sizes. Not replacing!");
                                    }
                                    continue;
                                }

                                Console.WriteLine("- - Looking for free space...");
                                var node = atlas.findSpaceForTexture(image.Width,image.Height);
                                if (node != null) {
                                    g.DrawImage(image, node.r.X, node.r.Y);
								    xml.Add(new XElement("SubTexture",
									    new XAttribute("name", name),
                                        new XAttribute("x", node.r.X),
                                        new XAttribute("y", node.r.Y),
									    new XAttribute("width", image.Width),
									    new XAttribute("height", image.Height)
                                    ));
                                    Console.WriteLine("- - Added at (" + node.r.X + ", " + node.r.Y + ").");
                                    spritesPatched++;
                                } else {
                                    Console.WriteLine("- - Couldn't find a spot for this sprite!");
                                }
							}
                    baseImage.Save(originalFile + ".png");
				}
                xml.Save(originalFile + ".xml");
                Console.WriteLine("- Patched "+spritesPatched+" out of "+spritesTotal+" sprites.");
			}
            Console.WriteLine("Done. Press any key.");
		}

		public static int Main (string[] args)
        {
            int selector = 0;
            bool good = false;
            while (selector != 2) {
                Console.Clear();
                DrawTitle();
                Console.WriteLine("Choose an option. Enter a number and hit Enter:");
                Console.WriteLine("  1) Patch texture atlases");
                Console.WriteLine("  2) Exit");
                Console.Write(":: ");
                good = int.TryParse(Console.ReadLine(), out selector);
                if (good)
                {
                    switch (selector)
                    {
                        case 1:
                            PatchSprites();
                            break;
                        case 2:
                            return 0;
                        default:
                            Console.WriteLine("Invalid choice. Press any key.");
                            break;
                    }
                } else Console.WriteLine("Invalid choice. Press any key.");
                Console.ReadKey();
            }
            return 0;
		}

        private static void DrawTitle()
        {
            Console.WriteLine("+++ TOWERFALL ASCENSION RESOURCE PATCHER v0.2 +++");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("");
        }
	}
}
