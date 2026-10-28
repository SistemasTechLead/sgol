using System.Security.Cryptography;
using System.Text;

namespace Sgol.Web.Presentation.Components;

// Person identity, rather than the displayed name, keeps the motif stable across
// assignments, filtering and refreshes, including people who share a name.
public sealed record BoardPersonAvatar(int Palette, int Rotation, int X, int Y, int DotX, int DotY)
{
    public static BoardPersonAvatar For(Guid personId, string name)
    {
        var seed = personId == Guid.Empty ? name : personId.ToString("N");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new(hash[0] % 6, hash[1] % 4 * 90, 8 + hash[2] % 25,
            8 + hash[3] % 25, 7 + hash[4] % 27, 7 + hash[5] % 27);
    }
}
