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
		public static void PatchSprites(bool cleanInstall = false)
		{
            Console.Clear();
            DrawTitle();
			foreach (var patchFile in Directory.EnumerateDirectories(Path.Combine("patchfiles", "sprites"))) {
                var atlasName = new DirectoryInfo(patchFile).Name;
                Console.WriteLine("Patching atlas \"" + atlasName + "\"...");

                var originalFile = Path.Combine("Content", "Atlas", atlasName);
                if (!File.Exists(originalFile + ".png") || !File.Exists(originalFile + ".xml"))
                {
                    Console.WriteLine("- This atlas does not exist!");
                    continue;
                }

                var backupFile = Path.Combine("Original", "Content", "Atlas", atlasName);
                CheckForBackupDirectory(2);

                if (!File.Exists(backupFile + ".png"))
                {
                    Console.WriteLine("- Creating backups...");
                    File.Copy(originalFile + ".png", backupFile + ".png");
                    File.Copy(originalFile + ".xml", backupFile + ".xml");
                }

                var xmlfile = originalFile + ".xml";
                if (cleanInstall) xmlfile = backupFile + ".xml";
                var xml = XElement.Load(xmlfile);

                string[] files = Directory.GetFiles(patchFile, "*.png", SearchOption.AllDirectories);
                var spritesPatched = 0;
                var spritesTotal = 0;

                var bitmap = originalFile + ".png";
                if (cleanInstall) bitmap = backupFile + ".png";

                var baseImage = Bitmap.FromFile(bitmap);
                if (baseImage != null)
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

                    var w = baseImage.Width;
                    var h = baseImage.Height;

                    var g = Graphics.FromImage(baseImage);
                    if (g != null)
                    {
                        foreach (string file in files)
                            using (var image = Bitmap.FromFile(file))
                            {
                                var attempt = 1;
                                spritesTotal++;

                                string name = file.Substring(patchFile.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                                name = name.Substring(0, name.Length - ".png".Length);
                                Console.WriteLine("- Adding new sprite \"" + name + "\"...");

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
                                        Console.WriteLine("- - Different sizes. Not replacing! Do a clean install!");
                                    }
                                    continue;
                                }

                                while (attempt <= 3)
                                {
                                    attempt++;

                                    Console.WriteLine("- - Looking for free space...");
                                    var node = atlas.findSpaceForTexture(image.Width, image.Height);
                                    if (node != null)
                                    {
                                        g.DrawImage(image, new Rectangle(node.r.X, node.r.Y, image.Width, image.Height));
                                        xml.Add(new XElement("SubTexture",
                                            new XAttribute("name", name),
                                            new XAttribute("x", node.r.X),
                                            new XAttribute("y", node.r.Y),
                                            new XAttribute("width", image.Width),
                                            new XAttribute("height", image.Height)
                                        ));
                                        Console.WriteLine("- - Added at (" + node.r.X + ", " + node.r.Y + ").");
                                        spritesPatched++;
                                        attempt = 5;
                                        continue;
                                    }
                                    else
                                    {
                                        Console.WriteLine("- - Couldn't find a spot for this sprite!");
                                        Console.WriteLine("- - Attempting to resize the atlas...");
                                        h += image.Height;
                                        var tempBaseImage = new Bitmap(w, h);
                                        g.Dispose();
                                        g = Graphics.FromImage(tempBaseImage);

                                        g.DrawImage(baseImage, new Rectangle(0, 0, baseImage.Width, baseImage.Height));
                                        baseImage.Dispose();
                                        baseImage = tempBaseImage;
                                        atlas.height = h;
                                        g.Dispose();
                                        g = Graphics.FromImage(baseImage);
                                    }
                                }
                            }
                        baseImage.Save(originalFile + ".png");
                        xml.Save(originalFile + ".xml");
                        Console.WriteLine("- Patched " + spritesPatched + " out of " + spritesTotal + " sprites.");
                        g.Dispose();
                    } else Console.WriteLine("- There was a problem drawing to this atlas.");
                    baseImage.Dispose();
                } else Console.WriteLine("- There was a problem opening this atlas.");
			}
            Console.WriteLine("Done. Press any key.");
		}
        /// <summary>
        /// Insert new elements into XML files.
        /// </summary>
        public static void PatchXML(bool cleanInstall = false)
        {
            Console.Clear();
            DrawTitle();
            foreach (var patchFile in Directory.GetFiles(Path.Combine("patchfiles", "xml"), "*.xml", SearchOption.AllDirectories))
            {
                var xmlName = Path.GetFileName(patchFile);
                Console.WriteLine("Patching XML \"" + xmlName + "\"...");

                if (xmlName == "questTips.xml" || xmlName == "versusTips.xml")
                {
                    Console.WriteLine("- WARNING: The Tips XML files have no unique identifiers.");
                    Console.WriteLine("-          This operation might create duplicates.");
                }

                var originalFile = Path.Combine("Content", "Atlas", xmlName);
                if (!File.Exists(originalFile))
                {
                    Console.WriteLine("- This XML does not exist!");
                    continue;
                }

                var backupFile = Path.Combine("Original", "Content", "Atlas", xmlName);
                CheckForBackupDirectory(2);

                if (!File.Exists(backupFile))
                {
                    Console.WriteLine("- Creating backup...");
                    File.Copy(originalFile, backupFile);
                }

                var xmlfile = originalFile;
                if (cleanInstall) xmlfile = backupFile;
                var xml = XElement.Load(xmlfile);
                var patchxml = XElement.Load(patchFile);

                foreach (var elem in patchxml.Elements())
                {
                    if (xmlName != "questTips.xml" && xmlName != "versusTips.xml")
                    {
                        IEnumerable<XElement> exists =
                            from el in xml.Elements(elem.Name)
                            where (string)el.Attribute("id") == (string)elem.Attribute("id")
                            select el;
                        if (exists.Count() > 0)
                        {
                            Console.WriteLine("- Overwriting element \"" + elem.Name + "\" > \"" + elem.Attribute("id").Value + "\".");
                            exists.First().ReplaceWith(elem);
                        }
                        else
                        {
                            Console.WriteLine("- Adding element \"" + elem.Name + "\" > \"" + elem.Attribute("id").Value + "\".");
                            xml.Add(elem);
                        }
                    }
                    else
                    {
                        Console.WriteLine("- Adding a tip.");
                        xml.Add(elem);
                    }
                }

                xml.Save(originalFile);
                Console.WriteLine("- Added " + patchxml.Elements().Count() + " XML elements.");
            }
            Console.WriteLine("Done. Press any key.");
        }

		public static int Main (string[] args)
        {
            int selector = 0;
            bool good = false;
            while (selector != 5) {
                Console.Clear();
                DrawTitle();
                Console.WriteLine("Choose an option. Enter a number and hit Enter:");
                Console.WriteLine("  1) Add/replace new textures");
                Console.WriteLine("  2) Reinstall all textures (clean install)");
                Console.WriteLine("  3) Add/replace XML elements");
                Console.WriteLine("  4) Reinstall XML elements (clean install)");
                Console.WriteLine("  5) Exit");
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
                            PatchSprites(true);
                            break;
                        case 3:
                            PatchXML();
                            break;
                        case 4:
                            PatchXML(true);
                            break;
                        case 5:
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

        private static void CheckForBackupDirectory(int type)
        {
            var dir = "Original";
            switch (type)
            {
                case 0:     // root backup folder
                    break;
                case 1:     // Content
                    CheckForBackupDirectory(0);
                    dir = Path.Combine(dir,"Content");
                    break;
                case 2:     // Content/Atlas
                    CheckForBackupDirectory(1);
                    dir = Path.Combine(dir,"Content","Atlas");
                    break;
            }
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
	}
}
