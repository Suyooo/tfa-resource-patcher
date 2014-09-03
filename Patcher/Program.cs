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
using System.Security.Cryptography;
using System.Diagnostics;

namespace ResourcePatcher
{
    public class ResourcePatcher
	{
		/// <summary>
		/// Insert new sprites into Atlas.
		/// </summary>
		public static void PatchSprites(bool cleanInstall = false)
		{
            Console.Clear();
            DrawTitle();
            if (!Directory.Exists(Path.Combine("patchfiles", "sprites")))
            {
                Console.WriteLine("There's no texture atlases to be patched.");
                return;
            }

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

                if (CheckForUpdate(originalFile + ".png") || CheckForUpdate(originalFile + ".xml"))
                {
                    Console.WriteLine("- The atlas has been modified outside of the patcher.");
                    Console.WriteLine("- This usually means there was an update for TFA.");
                    Console.WriteLine("- Reset the atlas and reinstall all sprites?");
                    Console.WriteLine("- Not doing this might result in TFA crashing.");
                    bool cont = false;
                    while (!cont)
                    {
                        Console.Write("- (yes/no): ");
                        string ret = Console.ReadLine();
                        cont = true;
                        if (ret.StartsWith("y", true, System.Globalization.CultureInfo.CurrentCulture))
                        {
                            Console.WriteLine("- Deleting backups...");
                            File.Delete(backupFile + ".png");
                            File.Delete(backupFile + ".xml");
                            cleanInstall = true;
                        }
                        else if (!ret.StartsWith("n", true, System.Globalization.CultureInfo.CurrentCulture))
                        {
                            Console.WriteLine("- Not a valid input.");
                            cont = false;
                        }
                    }
                }

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
                        CreateMD5File(originalFile + ".png");
                        CreateMD5File(originalFile + ".xml");
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
            if (!Directory.Exists(Path.Combine("patchfiles", "xml")))
            {
                Console.WriteLine("There's no XML files to be patched.");
                return;
            }

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

                if (CheckForUpdate(originalFile))
                {
                    Console.WriteLine("- The XML has been modified outside of the patcher.");
                    Console.WriteLine("- This usually means there was an update for TFA.");
                    Console.WriteLine("- Reset the XML file and reinstall all elements?");
                    Console.WriteLine("- Not doing this might result in TFA crashing.");
                    bool cont = false;
                    while (!cont)
                    {
                        Console.Write("- (yes/no): ");
                        string ret = Console.ReadLine();
                        cont = true;
                        if (ret.StartsWith("y", true, System.Globalization.CultureInfo.CurrentCulture))
                        {
                            Console.WriteLine("- Deleting backup...");
                            File.Delete(backupFile);
                            cleanInstall = true;
                        }
                        else if (!ret.StartsWith("n", true, System.Globalization.CultureInfo.CurrentCulture))
                        {
                            Console.WriteLine("- Not a valid input.");
                            cont = false;
                        }
                    }
                }

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
                CreateMD5File(originalFile);
            }
            Console.WriteLine("Done. Press any key.");
        }

        public static void ResetAll()
        {
            Console.Clear();
            DrawTitle();
            Console.WriteLine("This will reset all files to their original state");
            Console.WriteLine("and remove all backups. Use this function when a");
            Console.WriteLine("update messed up and you can't fix it with a clean");
            Console.WriteLine("install. All modifications to the game files, both");
            Console.WriteLine("from this patcher and external programs, are lost.");
            Console.WriteLine("Continue?");
            bool cont = false;
            while (!cont)
            {
                Console.Write("- (yes/no): ");
                string ret = Console.ReadLine();
                cont = true;
                if (ret.StartsWith("n", true, System.Globalization.CultureInfo.CurrentCulture))
                {
                    Console.WriteLine("Stopped. Press any key.");
                    return;
                }
                else if (!ret.StartsWith("y", true, System.Globalization.CultureInfo.CurrentCulture))
                {
                    Console.WriteLine("Not a valid input.");
                    cont = false;
                }
            }
            Console.WriteLine("");
            Console.WriteLine("Removing all backups...");
            Directory.Delete("Original", true);
            Console.WriteLine("Telling Steam to validate files...");
            Process.Start("steam://validate/251470");
            Console.WriteLine("");
            Console.WriteLine("Steam is now reacquiring the original game files.");
            Console.WriteLine("Please wait until it's finished before patching.");
            Console.WriteLine("Press any key.");
        }

		public static int Main (string[] args)
        {
            int selector = 0;
            bool good = false;

            while (selector != 6) {
                Console.Clear();
                DrawTitle();
                Console.WriteLine("Choose an option. Enter a number and hit Enter:");
                Console.WriteLine("  1) Add/replace new textures");
                Console.WriteLine("  2) Reinstall all textures (clean install)");
                Console.WriteLine("  3) Add/replace XML elements");
                Console.WriteLine("  4) Reinstall XML elements (clean install)");
                Console.WriteLine("  5) Reset all game files");
                Console.WriteLine("  6) Exit");
                Console.Write("(1-6): ");
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
                            ResetAll();
                            break;
                        case 6:
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
            Console.WriteLine("+++ TOWERFALL ASCENSION RESOURCE PATCHER v0.3 +++");
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

        private static bool CheckForUpdate(string file)
        {
            if (!File.Exists(file + ".md5")) return false;
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    string hash = System.Text.Encoding.UTF8.GetString(md5.ComputeHash(stream));
                    string comp = System.IO.File.ReadAllText(file + ".md5");
                    if (comp == hash) return false;
                    else return true;
                }
            }
        }

        private static void CreateMD5File(string file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    string hash = System.Text.Encoding.UTF8.GetString(md5.ComputeHash(stream));
                    System.IO.File.WriteAllText(file + ".md5", hash);
                }
            }
        }
	}
}
