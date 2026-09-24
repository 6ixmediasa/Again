using Again.Core;
using Xunit;
namespace Again.Tests;
public class VisualTests {
 [Fact]public void MatchesTranslatedTarget(){var random=new Random(42);var image=new byte[50*50];random.NextBytes(image);var target=new byte[8*8];for(int y=0;y<8;y++)Array.Copy(image,(19+y)*50+13,target,y*8,8);var match=VisualMatching.Match(image,50,50,target,8,8);Assert.Equal(13,match.X);Assert.Equal(19,match.Y);Assert.True(match.Reliable());}
 [Fact]public void AmbiguousTargetsRejected(){var target=new byte[]{0,100,220,180,40,255,90,170,20};var image=new byte[20*20];for(int y=0;y<3;y++){Array.Copy(target,y*3,image,(2+y)*20+2,3);Array.Copy(target,y*3,image,(12+y)*20+12,3);}var match=VisualMatching.Match(image,20,20,target,3,3);Assert.False(match.Reliable());}
 [Fact]public void UniformTargetRejected()=>Assert.Throws<InvalidDataException>(()=>VisualMatching.Match(new byte[100],10,10,new byte[9],3,3));
}
