using Xunit;
using Ara3D.BFast;
using System.Text;

namespace BFastTests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var bb = new BFastBuilder();
            var empty = bb.ToBFast();
            Assert.Equal(0, empty.Header.NumArrays);
            Assert.Empty(empty.Ranges);
            Assert.Equal(0, empty.Count);
            var hello = "Hello";
            bb.Add(hello);
            var oneArray = bb.ToBFast();
            Assert.Equal(1, oneArray.Header.NumArrays);
            Assert.Single(oneArray.Ranges);
            Assert.Equal(1, oneArray.Count);
            Assert.Equal(0, empty.Header.NumArrays);
            Assert.Empty(empty.Ranges);
            Assert.Equal(0, empty.Count);
            var xs = oneArray[0];
            var s = Encoding.UTF8.GetString(xs);
            Assert.Equal(s, hello);
        }
    }
}
