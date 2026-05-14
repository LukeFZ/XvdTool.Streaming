namespace LibXboxOne.XVC2.SerializedModel;

public interface IRootSerialize : ISerialize
{
    string OpcPath { get; }
    string OpcRelationship { get; }
}