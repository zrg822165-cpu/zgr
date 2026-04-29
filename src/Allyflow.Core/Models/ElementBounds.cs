namespace Allyflow.Core.Models;

public sealed record ElementBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;
}
