using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestStream()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("a;b;c\n\"\"\"\";a'b;'"));
            var lines = CsvReader.ReadFromStream(stream).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }
    }
}