//#define Trace

// Tar.cs
//
// version: 1.4.0.2
//    - built more flexibility into the checksum verification
//      to handle non-compliant checksums .
//
// version: 1.4.0.1
//    - now supports Gnu LongName entries and reads/writes GZIP'd tar
//      files (tgz).
//
// ------------------------------------------------------------------
//
// This is source code for a library/EXE for reading tar files.
// See doc for the unix TAR format on
//       http://www.mkssoftware.com/docs/man4/tar.4.asp
//
// Requirements:
//   .NET 3.5
//
// You can build this as either a Tar library, suitable for use within
// any application, or a standalone executable, suitable for use as a
// console application.
//
// To build the exe:
//
//   csc.exe /t:exe /debug+ /define:EXE  /out:Tar.exe Tar.cs
//
// To build the dll:
//
//   csc.exe /t:library /debug+ /out:Tar.dll Tar.cs
//
// ------------------------------------------------------------------
//
// Bugs:
//
//  - does not read or write bzip compressed tarballs  (.tar.bz2)
//  - does not archive symbolic links.
//  - uses Marshal.StructureToPtr and thus requires a LinkDemand, full trust.
//
// ------------------------------------------------------------------
//
// Copyright (c) 2009-2011 by Dino Chiesa
// All rights reserved!
//
// This program is licensed under the Microsoft Public License (Ms-PL)
// See the accompanying License.txt file for details.
//
// ------------------------------------------------------------------
//
// compile: csc.exe /t:exe /debug+ /define:EXE  /out:Tar.exe Tar.cs
//

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;



namespace Ionic
{
    /// <summary>
    ///   A class to create, list, or extract TAR archives. This is the
    ///   primary, central class for the Tar library.
    /// </summary>
    ///
    /// <remarks>
    /// Bugs:
    /// <list type="bullet">
    ///   <item> does not read or write bzip2 compressed tarballs  (.tar.bz2)</item>
    ///   <item> uses Marshal.StructureToPtr and thus requires a LinkDemand, full trust.d </item>
    /// </list>
    /// </remarks>
    public class Tar
    {
        /// <summary>
        /// Specifies the options to use for tar creation or extraction
        /// </summary>
        public class Options
        {
            /// <summary>
            /// The compression to use.  Applies only during archive
            /// creation. Ignored during extraction.
            /// </summary>
            public TarCompression Compression
            {
                get; set;
            }

            /// <summary>
            ///   A TextWriter to which verbose status messages will be
            ///   written during operation.
            /// </summary>
            /// <remarks>
            ///   <para>
            ///     Use this to see messages emitted by the Tar logic.
            ///     You can use this whether Extracting or creating an archive.
            ///   </para>
            /// </remarks>
            /// <example>
            ///   <code lang="C#">
            ///     var options = new Tar.Options();
            ///     options.StatusWriter = Console.Out;
            ///     Ionic.Tar.Extract("Archive2.tgz", options);
            ///   </code>
            /// </example>
            public TextWriter StatusWriter
            {
                get; set;
            }

            /// <summary>
            /// Whether to follow symbolic links when creating archives.
            /// </summary>
            public bool FollowSymLinks
            {
                get; set;
            }

            /// <summary>
            /// Whether to overwrite existing files when extracting archives.
            /// </summary>
            public bool Overwrite
            {
                get; set;
            }

            /// <summary>
            /// If true, the modified times of the extracted entries is
            /// NOT set according to the time set in the archive.  By
            /// default, the modified time is set.
            /// </summary>
            public bool DoNotSetTime
            {
                get; set;
            }
        }



        /// <summary>
        ///   Represents an entry in a TAR archive.
        /// </summary>
        public enum TarCompression
        {
            /// <summary>
            ///   No compression - just a vanilla tar.
            /// </summary>
            None = 0,
            /// <summary>
            ///   GZIP compression is applied to the tar to produce a .tgz file
            /// </summary>
            GZip,
        }

        /// <summary>
        ///   Represents an entry in a TAR archive.
        /// </summary>
        public class TarEntry
        {
            /// <summary>Intended for internal use only.</summary>
            internal TarEntry() { }
            /// <summary>The name of the file contained within the entry</summary>
            public string Name
            {
                get;
                internal set;
            }
            /// <summary>
            /// The size of the file contained within the entry. If the
            /// entry is a directory, this is zero.
            /// </summary>
            public int Size
            {
                get;
                internal set;
            }

            /// <summary>The last-modified time on the file or directory.</summary>
            public DateTime Mtime
            {
                get;
                internal set;
            }

            /// <summary>the type of the entry.</summary>
            public TarEntryType @Type
            {
                get;
                internal set;
            }

            /// <summary>a char representation of the type of the entry.</summary>
            public char TypeChar
            {
                get
                {
                    switch(@Type)
                    {
                        case TarEntryType.File_Old:
                        case TarEntryType.File:
                        case TarEntryType.File_Contiguous:
                            return 'f';
                        case TarEntryType.HardLink:
                            return 'l';
                        case TarEntryType.SymbolicLink:
                            return 's';
                        case TarEntryType.CharSpecial:
                            return 'c';
                        case TarEntryType.BlockSpecial:
                            return 'b';
                        case TarEntryType.Directory:
                            return 'd';
                        case TarEntryType.Fifo:
                            return 'p';
                        case TarEntryType.GnuLongLink:
                        case TarEntryType.GnuLongName:
                        case TarEntryType.GnuSparseFile:
                        case TarEntryType.GnuVolumeHeader:
                            return (char)(@Type);
                        default: return '?';
                    }
                }
            }
        }

        ///<summary>the type of Tar Entry</summary>
        public enum TarEntryType : byte
        {
            ///<summary>a file (old version)</summary>
            File_Old = 0,
            ///<summary>a file</summary>
            File = 48,
            ///<summary>a hard link</summary>
            HardLink = 49,
            ///<summary>a symbolic link</summary>
            SymbolicLink = 50,
            ///<summary>a char special device</summary>
            CharSpecial = 51,
            ///<summary>a block special device</summary>
            BlockSpecial = 52,
            ///<summary>a directory</summary>
            Directory = 53,
            ///<summary>a pipe</summary>
            Fifo = 54,
            ///<summary>Contiguous file</summary>
            File_Contiguous = 55,
            ///<summary>a GNU Long name?</summary>
            GnuLongLink = (byte)'K',    // "././@LongLink"
            ///<summary>a GNU Long name?</summary>
            GnuLongName = (byte)'L',    // "././@LongLink"
            ///<summary>a GNU sparse file</summary>
            GnuSparseFile = (byte)'S',
            ///<summary>a GNU volume header</summary>
            GnuVolumeHeader = (byte)'V',
        }


        // Numeric values are encoded in octal numbers using ASCII digits, with
        // leading zeroes. For historical reasons, a final null or space
        // character should be used. Thus although there are 12 bytes reserved
        // for storing the file size, only 11 octal digits can be stored.

        /// <summary>
        ///  This class is intended for internal use only, by the Tar library.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size=512)]
        internal struct HeaderBlock
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public byte[]   name;    // name of file. A directory is indicated by a trailing slash (/) in its name.

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[]   mode;    // file mode

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[]   uid;     // owner user ID

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[]   gid;     // owner group ID

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[]   size;    // length of file in bytes, encoded as octal digits in ASCII

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[]   mtime;   // modify time of file

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[]   chksum;  // checksum for header (use all blanks for chksum itself, when calculating)

            // The checksum is calculated by taking the sum of the
            // unsigned byte values of the header block with the eight
            // checksum bytes taken to be ascii spaces (decimal value
            // 32).

            // It is stored as a six digit octal number with leading
            // zeroes followed by a null and then a space.

            public byte     typeflag; // type of file

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public byte[]   linkname; // name of linked file (only if typeflag = '2')

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[]   magic;    // USTAR indicator

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[]   version;  // USTAR version

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[]   uname;    // owner user name

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[]   gname;    // owner group name

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[]   devmajor; // device major number

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[]   devminor; // device minor number

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 155)]
            public byte[]   prefix;   // prefix for file name

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[]   pad;     // ignored

            public static HeaderBlock CreateHeaderBlock()
            {
                HeaderBlock hb = new HeaderBlock
                    {
                        name     = new byte[100],
                        mode     = new byte[8],
                        uid      = new byte[8],   // owner user ID
                        gid      = new byte[8],   // owner group ID
                        size     = new byte[12],  // length of file in bytes, encoded in octal
                        mtime    = new byte[12],  // modify time of file
                        chksum   = new byte[8],   // checksum for header
                        linkname = new byte[100], // name of linked file
                        magic    = new byte[6],   // USTAR indicator
                        version  = new byte[2],   // USTAR version
                        uname    = new byte[32],  // owner user name
                        gname    = new byte[32],  // owner group name
                        devmajor = new byte[8],   // device major number
                        devminor = new byte[8],   // device minor number
                        prefix   = new byte[155], // prefix for file name
                        pad      = new byte[12],  // ignored
                    };

                Array.Copy(System.Text.Encoding.ASCII.GetBytes("ustar "), 0, hb.magic, 0, 6 );
                hb.version[0]=hb.version[1]=(byte) TarEntryType.File;

                return hb;
            }


            public bool VerifyChksum()
            {

                int stored = GetChksum();
                int calculated = SetChksum();
                TraceOutput("stored({0})  calc({1})", stored, calculated);

                return (stored == calculated);
            }


            public int GetChksum()
            {
                TraceData("chksum", this.chksum);

                // The tar spec says that the Checksum must be stored as a
                // six digit octal number with leading zeroes followed by a
                // null and then a space.

                // Various implementations do not adhere to this, so reader
                // programs should be flexible.

                // A more compatible approach may be to use the
                // first white-space-trimmed six digits for checksum.
                //
                // In addition, some older tar implementations treated
                // bytes as signed. Readers must calculate the checksum
                // both ways, and treat it as good if either the signed
                // or unsigned sum matches the included checksum.

                // special case
                bool allZeros = true;
                Array.ForEach(this.chksum, (x) => {if (x!=0) allZeros= false; });
                if (allZeros) return 256;

                // validation 6 and 7 have to be 0 and 0x20, in some order.
                if (!(((this.chksum[6]==0) && (this.chksum[7]==0x20)) ||
                    ((this.chksum[7]==0) && (this.chksum[6]==0x20))))
                    return -1;

                string v = System.Text.Encoding.ASCII.GetString(this.chksum, 0, 6).Trim();
                TraceOutput("chksum string: '{0}'", v);
                return Convert.ToInt32(v, 8);
            }


            public int SetChksum()
            {
                // first set the checksum to all ASCII _space_ (dec 32)
                var a = System.Text.Encoding.ASCII.GetBytes(new String(' ',8));
                Array.Copy(a, 0, this.chksum, 0, a.Length);  // always 8

                // then sum all the bytes
                int rawSize = 512;
                IntPtr buffer = Marshal.AllocHGlobal( rawSize );
                Marshal.StructureToPtr( this, buffer, false );
                byte[] block = new byte[ rawSize ];
                Marshal.Copy( buffer, block, 0, rawSize );
                Marshal.FreeHGlobal( buffer );

                // format as octal
                int sum= 0;
                Array.ForEach(block, (x) => sum+=x );
                string s = "000000" + Convert.ToString(sum, 8);

                // put that into the checksum block
                a = System.Text.Encoding.ASCII.GetBytes(s.Substring(s.Length-6));
                Array.Copy(a, 0, this.chksum, 0, a.Length);  // always 6
                this.chksum[6]=0;
                this.chksum[7]=0x20;

                return sum;
            }


            public void SetSize(int sz)
            {
                string ssz = String.Format("          {0} ", Convert.ToString(sz, 8));
                // get last 12 chars
                var a = System.Text.Encoding.ASCII.GetBytes(ssz.Substring(ssz.Length-12));
                Array.Copy(a, 0, this.size, 0, a.Length);  // always 12
            }

            public int GetSize()
            {
                return Convert.ToInt32(System.Text.Encoding.ASCII.GetString(this.size).TrimNull(), 8);
            }

            public void InsertLinkName(string linkName)
            {
                // if greater than 100, then an exception occurs
                var a = System.Text.Encoding.ASCII.GetBytes(linkName);
                Array.Copy(a, 0, this.linkname, 0, a.Length);
            }

            public void InsertName(string itemName)
            {
                if (itemName.Length <= 100)
                {
                    var a = System.Text.Encoding.ASCII.GetBytes(itemName);
                    Array.Copy(a, 0, this.name, 0, a.Length);
                }
                else
                {
                    var a = System.Text.Encoding.ASCII.GetBytes(itemName);
                    Array.Copy(a, a.Length-100, this.name, 0, 100);
                    Array.Copy(a, 0, this.prefix, 0, a.Length-100);
                }

                // insert the modified time for the file or directory, also
                DateTime dt = File.GetLastWriteTimeUtc(itemName);
                int time_t = TimeConverter.DateTime2TimeT(dt);
                string mtime = "     " + Convert.ToString(time_t, 8) + " ";
                var a1 = System.Text.Encoding.ASCII.GetBytes(mtime.Substring(mtime.Length-12));
                Array.Copy(a1, 0, this.mtime, 0, a1.Length); // always 12
            }


            public DateTime GetMtime()
            {
                int time_t = Convert.ToInt32(System.Text.Encoding.ASCII.GetString(this.mtime).TrimNull(), 8);
                return DateTime.SpecifyKind(TimeConverter.TimeT2DateTime(time_t), DateTimeKind.Utc);
            }

            public string GetName()
            {
                string n = null;
                string m = GetMagic();
                if (m != null && m.Equals("ustar"))
                {
                    n = (this.prefix[0]==0)
                        ? System.Text.Encoding.ASCII.GetString(this.name).TrimNull()
                        : System.Text.Encoding.ASCII.GetString(this.prefix).TrimNull() + System.Text.Encoding.ASCII.GetString(this.name).TrimNull();
                }
                else
                {
                    n = System.Text.Encoding.ASCII.GetString(this.name).TrimNull();
                }
                return n;
            }


            private string GetMagic()
            {
                string m = (this.magic[0]==0) ? null : System.Text.Encoding.ASCII.GetString(this.magic).Trim();
                return m;
            }

        }

        private Options TarOptions { get; set; }

        private Tar () {}

        /// <summary>
        ///   Extract the named tar archive to the current directory
        /// </summary>
        /// <param name="archive">
        ///   The name of the tar archive to extract.
        /// </param>
        /// <returns>
        ///   A <c>ReadOnlyCollection</c> of TarEntry instances contained within
        ///   the archive.
        /// </returns>
        /// <example>
        ///   <code lang="VB">
        ///     ' extract a regular tar archive, placing files in the current dir:
        ///     Ionic.Tar.Extract("MyArchive.tar")
        ///     ' extract a compressed tar archive, placing files in the current dir:
        ///     Ionic.Tar.Extract("Archive2.tgz")
        ///   </code>
        ///   <code lang="C#">
        ///     // extract a regular tar archive, placing files in the current dir:
        ///     Ionic.Tar.Extract("MyArchive.tar");
        ///     // extract a compressed tar archive, placing files in the current dir:
        ///     Ionic.Tar.Extract("Archive2.tgz")
        ///   </code>
        /// </example>
        public static System.Collections.ObjectModel.ReadOnlyCollection<TarEntry>
            Extract(string archive)
        {
            return ListOrExtract(archive, true, null).AsReadOnly();
        }

        /// <summary>
        ///   Extract the named tar archive to the current directory
        /// </summary>
        /// <param name="archive">
        ///   The name of the tar archive to extract.
        /// </param>
        /// <param name="options">
        ///   A set of options for extracting.
        /// </param>
        /// <returns>
        ///   A <c>ReadOnlyCollection</c> of TarEntry instances
        ///   contained within the archive.
        /// </returns>
        public static System.Collections.ObjectModel.ReadOnlyCollection<TarEntry>
            Extract(string archive, Options options)
        {
            return ListOrExtract(archive, true, options).AsReadOnly();
        }

        /// <summary>
        ///   Get a list of the TarEntry items contained within the named archive.
        /// </summary>
        /// <param name="archive">
        ///   The name of the tar archive.
        /// </param>
        /// <returns>
        ///   A <c>ReadOnlyCollection</c> of TarEntry instances
        ///   contained within the archive.
        /// </returns>
        ///
        /// <example>
        /// <code lang="C#">
        /// private void ListContents(string archiveName)
        /// {
        ///     var list = Ionic.Tar.List(archiveName);
        ///     foreach (var item in list)
        ///     {
        ///         Console.WriteLine("{0,-20}  {1,9}  {2}",
        ///                           item.Mtime.ToString("u"),
        ///                           item.Size, item.Name);
        ///     }
        ///     Console.WriteLine(new String('-', 66));
        ///     Console.WriteLine("                                 {0} entries",
        ///                       list.Count);
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Private Sub ListContents(ByVal archiveName as String)
        ///     Dim list As System.Collections.ObjectModel.ReadOnlyCollection(Of Ionic.Tar.TarEntry) = _
        ///         Ionic.Tar.List(archiveName)
        ///     Dim item As Ionic.Tar.TarEntry
        ///     For Each s In list
        ///         Console.WriteLine("{0,-20}  {1,9}  {2}", _
        ///                           item.Mtime.ToString("u"), _
        ///                           item.Size, item.Name)
        ///     Next
        ///     Console.WriteLine(New String("-"c, 66))
        ///     Console.WriteLine("                                 {0} entries", _
        ///                       list.Count)
        /// End Sub
        /// </code>
        /// </example>
        public static System.Collections.ObjectModel.ReadOnlyCollection<TarEntry>
            List(string archive)
        {
            return ListOrExtract(archive, false, null).AsReadOnly();
        }

        private static List<TarEntry> ListOrExtract(string archive,
                                                    bool wantExtract,
                                                    Options options)
        {
            var t = new Tar();
            t.TarOptions = options ?? new Options();
            return t._internal_ListOrExtract(archive, wantExtract);
        }


        [DllImport("kernel32.dll", EntryPoint="CreateSymbolicLinkW", CharSet=CharSet.Unicode)]
        private static extern int CreateSymbolicLink(string symlinkFileName,
                                                     string targetFileName, int flags);


        private List<TarEntry> _internal_ListOrExtract(string archive, bool wantExtract)
        {
            var entryList = new List<TarEntry>();
            byte[] block = new byte[512];
            int n = 0;
            int blocksToMunch = 0;
            int remainingBytes = 0;
            Stream output= null;
            DateTime mtime = DateTime.Now;
            string name = null;
            TarEntry entry = null;
            var deferredDirTimestamp= new Dictionary<String,DateTime>();

            if (!File.Exists(archive))
                throw new InvalidOperationException("The specified file does not exist.");

            using (Stream fs = _internal_GetInputStream(archive))
            {
                while ((n = fs.Read(block, 0, block.Length)) > 0)
                {
                    if (blocksToMunch > 0)
                    {
                        if (output!=null)
                        {
                            int bytesToWrite = (block.Length < remainingBytes)
                                ? block.Length
                                : remainingBytes;

                            output.Write(block, 0, bytesToWrite);
                            remainingBytes -= bytesToWrite;
                        }

                        blocksToMunch--;

                        //System.Diagnostics.Debugger.Break();

                        if (blocksToMunch == 0)
                        {
                            if (output!= null)
                            {
                                if (output is MemoryStream)
                                {
                                    entry.Name = name = System.Text.Encoding.ASCII.GetString((output as MemoryStream).ToArray()).TrimNull();
                                }

                                output.Close();
                                output.Dispose();

                                if (output is FileStream && !TarOptions.DoNotSetTime)
                                {
                                    File.SetLastWriteTimeUtc(name, mtime);
                                }

                                output = null;
                            }
                        }
                        continue;
                    }

                    HeaderBlock hb = serializer.RawDeserialize(block);

                    //System.Diagnostics.Debugger.Break();

                    if (!hb.VerifyChksum())
                        throw new Exception("header checksum is invalid.");

                    // if this is the first entry, or if the prior entry is not a GnuLongName
                    if (entry==null || entry.Type!=TarEntryType.GnuLongName)
                        name = hb.GetName();

                    if (name== null || name.Length == 0) break; // EOF
                    mtime = hb.GetMtime();
                    remainingBytes = hb.GetSize();

                    if (hb.typeflag==0) hb.typeflag=(byte)'0'; // coerce old-style GNU type to posix tar type

                    entry = new TarEntry() {Name = name, Mtime = mtime, Size = remainingBytes, @Type = (TarEntryType)hb.typeflag } ;

                    if (entry.Type!=TarEntryType.GnuLongName)
                        entryList.Add(entry);

                    blocksToMunch = (remainingBytes > 0)
                        ? ((remainingBytes - 1) / 512) +1
                        : 0;

                    if (entry.Type==TarEntryType.GnuLongName)
                    {
                        if (name != "././@LongLink")
                        {
                            if (wantExtract)
                                throw new Exception(String.Format("unexpected name for type 'L' (expected '././@LongLink', got '{0}')", name));
                        }
                        // for GNU long names, we extract the long name info into a memory stream
                        if (output != null) output.Close();
                        output = new MemoryStream();
                        continue;
                    }

                    if (wantExtract)
                    {
                        switch (entry.Type)
                        {
                            case TarEntryType.Directory:
                                if (!Directory.Exists(name))
                                {
                                    Directory.CreateDirectory(name);
                                    // cannot set the time on the directory now, or it will be updated
                                    // by future file writes.  Defer until after all file writes are done.
                                    if (!TarOptions.DoNotSetTime)
                                        deferredDirTimestamp.Add(name.TrimSlash(), mtime);
                                }
                                else if (TarOptions.Overwrite)
                                {
                                    if (!TarOptions.DoNotSetTime)
                                        deferredDirTimestamp.Add(name.TrimSlash(), mtime);
                                }
                                break;

                            case TarEntryType.File_Old:
                            case TarEntryType.File:
                            case TarEntryType.File_Contiguous:
                                string p = Path.GetDirectoryName(name);
                                if (!String.IsNullOrEmpty(p))
                                {
                                    if (!Directory.Exists(p))
                                        Directory.CreateDirectory(p);
                                }
                                if (output != null) output.Close();
                                output = _internal_GetExtractOutputStream(name);
                                break;

                            case TarEntryType.GnuVolumeHeader:
                            case TarEntryType.CharSpecial:
                            case TarEntryType.BlockSpecial:
                                // do nothing on extract
                                break;

                            case TarEntryType.SymbolicLink:
                                break;
                                // can support other types here - links, etc


                            default:
                                throw new Exception(String.Format("unsupported entry type ({0})", hb.typeflag));
                        }
                    }
                }
            }

            // apply the deferred timestamps on the directories
            if (deferredDirTimestamp.Count > 0)
            {
                foreach (var s in deferredDirTimestamp.Keys)
                {
                    Directory.SetLastWriteTimeUtc(s, deferredDirTimestamp[s]);
                }
            }

            if (output != null) output.Close();
            return entryList;
        }


        private Stream _internal_GetInputStream(string archive)
        {
            if (archive.EndsWith(".tgz") || archive.EndsWith(".tar.gz"))
            {
                var fs = File.Open(archive, FileMode.Open, FileAccess.Read);
                return new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress, false);
            }

            return File.Open(archive, FileMode.Open, FileAccess.Read);
        }

        private Stream _internal_GetExtractOutputStream(string name)
        {
            if (TarOptions.Overwrite || !File.Exists(name))
            {
                if (TarOptions.StatusWriter != null)
                    TarOptions.StatusWriter.WriteLine("{0}", name);
                return File.Open(name, FileMode.Create, FileAccess.ReadWrite);
            }

            if (TarOptions.StatusWriter != null)
                TarOptions.StatusWriter.WriteLine("{0} (not overwriting)", name);

            return null;
        }


        /// <summary>
        ///   Create a tar archive with the given name, and containing
        ///   the given set of files or directories.
        /// </summary>
        /// <param name="outputFile">
        ///   The name of the tar archive to create. The file must not
        ///   exist at the time of the call.
        /// </param>
        /// <param name="filesOrDirectories">
        ///   A list of filenames and/or directory names to be added to the archive.
        /// </param>
        ///
        /// <example>
        ///   <code lang="VB">
        ///     Ionic.Tar.CreateArchive("MyArchive.tar", _
        ///                             new String() {"file1.txt", "file2.txt"})
        ///   </code>
        ///   <code lang="C#">
        ///     Ionic.Tar.CreateArchive("MyArchive.tar",
        ///                             new String[] {"file1.txt", "file2.txt"});
        ///   </code>
        /// </example>
        public static void CreateArchive(string outputFile,
                                         IEnumerable<String> filesOrDirectories)
        {
            var t = new Tar();
            t._internal_CreateArchive(outputFile, filesOrDirectories);
        }

        /// <summary>
        ///   Create a tar archive with the given name, and containing
        ///   the given set of files or directories, and using the given options.
        /// </summary>
        /// <param name="outputFile">
        ///   The name of the tar archive to create. The file must not
        ///   exist at the time of the call.
        /// </param>
        /// <param name="filesOrDirectories">
        ///   A list of filenames and/or directory names to be added to the archive.
        /// </param>
        /// <param name="options">
        ///   The options to use during Tar operation.
        /// </param>
        public static void CreateArchive(string outputFile,
                                         IEnumerable<String> filesOrDirectories,
                                         Options options)
        {
            var t = new Tar();
            t.TarOptions = options;
            t._internal_CreateArchive(outputFile, filesOrDirectories);
        }


        private void _internal_CreateArchive(string outputFile, IEnumerable<String> files)
        {
            if (String.IsNullOrEmpty(outputFile))
                throw new InvalidOperationException("You must specify an output file.");
            if (File.Exists(outputFile))
                throw new InvalidOperationException("The output file you specified already exists.");
            if (Directory.Exists(outputFile))
                throw new InvalidOperationException("The output file you specified is a directory.");

            int fcount = 0;
            try
            {

                using (_outfs = _internal_GetOutputArchiveStream(outputFile))
                {
                    foreach (var f in files)
                    {
                        fcount++;

                        if (Directory.Exists(f))
                            AddDirectory(f);
                        else if (File.Exists(f))
                            AddFile(f);
                        else
                            throw new InvalidOperationException(String.Format("The file you specified ({0}) was not found.", f));
                    }

                    if (fcount < 1)
                        throw new InvalidOperationException("Specify one or more input files to place into the archive.");

                    // terminator
                    byte[] block = new byte[512];
                    _outfs.Write(block, 0, block.Length);
                    _outfs.Write(block, 0, block.Length);
                }
            }
            finally
            {
                if (fcount < 1)
                    try { File.Delete(outputFile); } catch { }
            }
        }


        private Stream _internal_GetOutputArchiveStream(string filename)
        {
            switch(TarOptions.Compression)
            {
                case TarCompression.None:
                    return File.Open(filename, FileMode.Create, FileAccess.ReadWrite);

                case TarCompression.GZip:
                    {
                        var fs = File.Open(filename, FileMode.Create, FileAccess.ReadWrite);
                        return new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Compress, false);
                    }

                default:
                    throw new Exception("bad state");
            }
        }



        private void AddDirectory(string dirName)
        {
            dirName = dirName.TrimVolume();

            // insure trailing slash
            if (!dirName.EndsWith("/"))
                dirName += "/";

            if (TarOptions.StatusWriter != null)
                TarOptions.StatusWriter.WriteLine("{0}", dirName);

            // add the block for the dir, right here.
            HeaderBlock hb = HeaderBlock.CreateHeaderBlock();
            hb.InsertName(dirName);
            hb.typeflag = 5 + (byte)'0' ;
            hb.SetSize(0); // some impls use agg size of all files contained
            hb.SetChksum();
            byte[] block = serializer.RawSerialize(hb);
            _outfs.Write(block, 0, block.Length);

            // add the files:
            String[] filenames = Directory.GetFiles(dirName);
            foreach (String filename in filenames)
            {
                AddFile(filename);
            }

            // add the subdirectories:
            String[] dirnames = Directory.GetDirectories(dirName);
            foreach (String d in dirnames)
            {
                // handle reparse points
                var a = System.IO.File.GetAttributes(d);
                if ((a & FileAttributes.ReparsePoint) == 0)
                    // not a symlink
                    AddDirectory(d);
                else if (this.TarOptions.FollowSymLinks)
                    // isa symlink, and we want to follow it
                    AddDirectory(d);
                else
                    // not following symlinks; add it
                    AddSymlink(d);
            }
        }



        private void AddSymlink(string name)
        {
            if (TarOptions.StatusWriter != null)
                TarOptions.StatusWriter.WriteLine("{0}", name);

            // add the block for the symlink, right here.
            HeaderBlock hb = HeaderBlock.CreateHeaderBlock();
            hb.InsertName(name);
            hb.InsertLinkName(name);
            hb.typeflag = (byte)TarEntryType.SymbolicLink;
            hb.SetSize(0);
            hb.SetChksum();
            byte[] block = serializer.RawSerialize(hb);
            _outfs.Write(block, 0, block.Length);
        }



        private void AddFile(string fileName)
        {
            // is it a symlink (ReparsePoint)?
            var a = System.IO.File.GetAttributes(fileName);
            if ((a & FileAttributes.ReparsePoint) != 0)
            {
                AddSymlink(fileName);
                return;
            }
            if (TarOptions.StatusWriter != null)
                TarOptions.StatusWriter.WriteLine("{0}", fileName);

            HeaderBlock hb = HeaderBlock.CreateHeaderBlock();
            hb.InsertName(fileName);
            hb.typeflag = (byte)TarEntryType.File;  // 0 + (byte)'0' ;
            FileInfo fi = new FileInfo(fileName);
            hb.SetSize((int)fi.Length);

            hb.SetChksum();
            byte[] block = serializer.RawSerialize(hb);
            _outfs.Write(block, 0, block.Length);

            using (FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.Read))
            {
                int n= 0;
                Array.Clear(block, 0, block.Length);
                while ((n = fs.Read(block, 0, block.Length)) > 0)
                {
                    _outfs.Write(block, 0, block.Length); // not n!!
                    Array.Clear(block, 0, block.Length);
                }
            }
        }


        private RawSerializer<HeaderBlock> _s;
        private RawSerializer<HeaderBlock> serializer
        {
            get
            {
                if (_s == null)
                    _s= new RawSerializer<HeaderBlock>();
                return _s;
            }
        }


        [System.Diagnostics.ConditionalAttribute("Trace")]
        private static void TraceData(string label, byte[] data)
        {
            Console.WriteLine("{0}:", label);
            Array.ForEach(data, (x) => Console.Write("{0:X} ", (byte)x));
            System.Console.WriteLine();
        }


        [System.Diagnostics.ConditionalAttribute("Trace")]
        private static void TraceOutput(string format, params object[] varParams)
        {
            Console.WriteLine(format, varParams);
        }


        // member variables
        private Stream   _outfs = null;
        //private System.Text.Encoding _ascii = System.Text.Encoding.ASCII;
    }



    /// <summary>
    ///  This class is intended for internal use only, by the Tar library.
    /// </summary>
    internal static class Extensions
    {
        public static string TrimNull(this string t)
        {
            return t.Trim( new char[] { (char)0x20, (char)0x00 } );
        }
        public static string TrimSlash(this string t)
        {
            return t.TrimEnd( new char[] { (char)'/' } );
        }

        public static string TrimVolume(this string t)
        {
            if (t.Length > 3 && t[1]==':' && t[2]=='/')
                return t.Substring(3);
            if (t.Length > 2 && t[0]=='/' && t[1]=='/')
                return t.Substring(2);
            return t;
        }
    }




    /// <summary>
    ///  This class is intended for internal use only, by the Tar library.
    /// </summary>
    internal static class TimeConverter
    {
        private static System.DateTime _unixEpoch = new System.DateTime(1970,1,1, 0,0,0, DateTimeKind.Utc);
        private static System.DateTime _win32Epoch = new System.DateTime(1601,1,1, 0,0,0, DateTimeKind.Utc);

        public static Int32 DateTime2TimeT(System.DateTime datetime)
        {
            System.TimeSpan delta =  datetime - _unixEpoch;
            return (System.Int32)(delta.TotalSeconds);
        }


        public static System.DateTime TimeT2DateTime(int timet)
        {
            return _unixEpoch.AddSeconds(timet);
        }

        public static Int64 DateTime2Win32Ticks(System.DateTime datetime)
        {
            System.TimeSpan delta =  datetime - _win32Epoch;
            return (Int64) (delta.TotalSeconds * 10000000L);
        }

        public static DateTime Win32Ticks2DateTime(Int64 ticks)
        {
            return _win32Epoch.AddSeconds(ticks/10000000);
        }
    }



    /// <summary>
    ///  This class is intended for internal use only, by the Tar library.
    /// </summary>
    internal class RawSerializer<T>
    {
        public T RawDeserialize( byte[] rawData )
        {
            return RawDeserialize( rawData , 0 );
        }

        public T RawDeserialize( byte[] rawData , int position )
        {
            int rawsize = Marshal.SizeOf( typeof(T) );
            if( rawsize > rawData.Length )
                return default(T);

            IntPtr buffer = Marshal.AllocHGlobal( rawsize );
            Marshal.Copy( rawData, position, buffer, rawsize );
            T obj = (T) Marshal.PtrToStructure( buffer, typeof(T) );
            Marshal.FreeHGlobal( buffer );
            return obj;
        }

        public byte[] RawSerialize( T item )
        {
            int rawSize = Marshal.SizeOf( typeof(T) );
            IntPtr buffer = Marshal.AllocHGlobal( rawSize );
            Marshal.StructureToPtr( item, buffer, false );
            byte[] rawData = new byte[ rawSize ];
            Marshal.Copy( buffer, rawData, 0, rawSize );
            Marshal.FreeHGlobal( buffer );
            return rawData;
        }
    }

    // (setq c-symbol-start "\\<[[:alpha:]_]")
    // (modify-syntax-entry ?# "w" csharp-mode-syntax-table)


    #if EXE

    public class TarApp
    {
        // ctor
        public TarApp () {}

        private bool           _verbose;
        private Tar.Options    _options;
        private bool           _expectFile;
        private string         _archiveName;
        private List<string>   _fileNames;
        private TarAction      _action;
        delegate void TarAction();

        private void ProcessOptionString(string arg)
        {
            _options = new Tar.Options();

            foreach (char c in arg.ToCharArray())
            {
                switch(c)
                {
                    case 'c':
                        if (_action != null) throw new ArgumentException("c");
                        _action = CreateArchive;
                        break;
                    case 'x':
                        if (_action != null) throw new ArgumentException("x");
                        _action = ExtractArchive;
                        break;
                    case 't':
                        if (_action != null) throw new ArgumentException("t");
                        _action = ListContents;
                        break;
                    case 'f':
                        if (_expectFile) throw new ArgumentException("f");
                        _expectFile = true;
                        break;
                    case 'k':
                        if (_options.Overwrite) throw new ArgumentException("k");
                        _options.Overwrite = true;
                        break;
                    case 'm':
                        if (_options.DoNotSetTime) throw new ArgumentException("m");
                        _options.DoNotSetTime = true;
                        break;
                    case 'L':
                        if (_options.FollowSymLinks) throw new ArgumentException("L");
                        _options.FollowSymLinks = true;
                        break;
                    case 'z':
                        if (_options.Compression!=Tar.TarCompression.None) throw new ArgumentException("z");
                        _options.Compression = Tar.TarCompression.GZip;
                        break;
                    case 'v':
                        if (_verbose) throw new ArgumentException("v");
                        _verbose = true;
                        _options.StatusWriter = System.Console.Out;
                        break;
                    default:
                        throw new ArgumentException(new String(c, 1));
                }
            }
        }



        public TarApp (string[] args)
        {
            if (args.Length ==0)
                Usage();

            ProcessOptionString((args[0][0]=='-') ? args[0].Substring(1) : args[0]);

            for (int i=1; i < args.Length; i++)
            {
                if (args[i]=="-?") Usage();
                if (_expectFile)
                {
                    if (_fileNames== null)
                    {
                        _fileNames = new List<String>();
                        _archiveName = args[i];
                    }
                    else
                    {
                        if (_action != CreateArchive)
                            throw new ArgumentException();

                        _fileNames.Add(args[i]);
                    }
                }
            }

            // validation
            if (String.IsNullOrEmpty(_archiveName))
                throw new ArgumentException();

            if (_action == null)
                throw new ArgumentException();

            if (_action == CreateArchive)
            {
                if (_options.Compression != Tar.TarCompression.None &&
                    !_archiveName.EndsWith(".tar.gz") &&
                    !_archiveName.EndsWith(".tgz"))
                    System.Console.Error.WriteLine("Warning: non-standard extension used on a compressed archive.");
                else if (_options.Compression == Tar.TarCompression.None &&
                         !_archiveName.EndsWith(".tar"))
                    System.Console.Error.WriteLine("Warning: non-standard extension used on an archive.");
            }
            else if (!_archiveName.EndsWith(".tar.gz") &&
                     !_archiveName.EndsWith(".tgz") &&
                     !_archiveName.EndsWith(".tar"))
                System.Console.Error.WriteLine("Warning: non-standard extension used on a compressed archive.");

        }



        public void Run()
        {
            _action();
        }


        private void ListContents()
        {
            var list = Ionic.Tar.List(_archiveName);
            foreach (var entry in list)
            {
                if (_verbose)
                    System.Console.WriteLine("{0} {1,-20}  {2,9}  {3}",
                                             entry.TypeChar,
                                             entry.Mtime.ToString("u"), entry.Size, entry.Name);
                else
                    System.Console.WriteLine("{0}", entry.Name);
            }
            if (_verbose)
            {
                System.Console.WriteLine(new String('-', 66));
                System.Console.WriteLine("                                 {0} entries", list.Count);
            }
        }


        private void CreateArchive()
        {
            Ionic.Tar.CreateArchive(_archiveName, _fileNames, _options);
        }


        private void ExtractArchive()
        {
            Ionic.Tar.Extract(_archiveName, _options);
        }


        public static void Usage()
        {
            Console.WriteLine("\nTar: process tar archives.\n\n" +
                              "Usage:\n  Tar [-] [x|c|t] [options] [tarfile] ...\n" +
                              "\n" +
                              "   tar -c ... creates an archive\n" +
                              "   tar -x ... extracts an archive\n" +
                              "   tar -t ... lists the contents of an archive\n" +
                              "options:\n" +
                              "  -k    (x mode only) Do not overwrite existing files.  In case a file\n" +
                              "        a file appears more than once in an archive, later copies will\n" +
                              "        not overwrite earlier copies.\n" +
                              "  -L    (c mode only) All symbolic links will be followed.\n" +
                              "        Normally, symbolic links (Reparse Points0 are ignored.  With \n" +
                              "        this option, the target of the link will be archived instead.\n" +
                              "  -m    (x mode only) Do not extract modification time.  By default, the\n"+
                              "        modification time is set to the time stored in the archive.\n" +
                              "  -v    emit verbose output during operation.\n" +
                              "  -z    (c mode only) the resulting archive will be GZIP compressed.\n" +
                              "\n" +
                              "Examples:\n" +
                              "\n" +
                              "   to list the entries in a tar archive:\n" +
                              "     tar -tvf  archive.tar\n" +
                              "\n" +
                              "   to silently extract a tar archive:\n" +
                              "     tar -xf  newarchive.tar \n" +
                              "\n" +
                              "   to verbosely create a tar archive, compressed with gzip:\n" +
                              "     tar -cvfz  newarchive.tgz dir1 file1 ..\n" +
                              "\n"
                              );

            System.Environment.Exit(1);


            // todo, other tar options:
            //      -U      (x mode only) Unlink files before creating them.  Without this
            //              option, tar overwrites existing files, which preserves existing
            //              hardlinks.  With this option, existing hardlinks will be broken,
            //              as will any symlink that would affect the location of an
            //              extracted file.
        }


        public static void Main(string[] args)
        {
            try
            {
                new TarApp(args)
                    .Run();
            }
            catch (System.Exception exc1)
            {
                Console.WriteLine("Exception: {0}", exc1.ToString());
                Usage();
            }
        }
    }


    #endif


}
