namespace LibXboxOne.XVC2;

public readonly record struct BoxIndex(int Value)
{
    public override string ToString() => $"box:{Value}";
}