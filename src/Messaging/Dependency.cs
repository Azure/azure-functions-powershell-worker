// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: ManagedDependency/Dependency.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Microsoft.Azure.WebJobs.Script.Grpc.Messages {

  /// <summary>Holder for reflection information generated from ManagedDependency/Dependency.proto</summary>
  public static partial class DependencyReflection {

    #region Descriptor
    /// <summary>File descriptor for ManagedDependency/Dependency.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static DependencyReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiJNYW5hZ2VkRGVwZW5kZW5jeS9EZXBlbmRlbmN5LnByb3RvEhlBenVyZUZ1",
            "bmN0aW9uc1JwY01lc3NhZ2VzIh0KCkRlcGVuZGVuY3kSDwoHZW5hYmxlZBgB",
            "IAEoCEKnAQoqY29tLm1pY3Jvc29mdC5henVyZS5mdW5jdGlvbnMucnBjLm1l",
            "c3NhZ2VzQg9EZXBlbmRlbmN5UHJvdG9QAVo3Z2l0aHViLmNvbS9BenVyZS9h",
            "enVyZS1mdW5jdGlvbnMtZ28td29ya2VyL2ludGVybmFsL3JwY6oCLE1pY3Jv",
            "c29mdC5BenVyZS5XZWJKb2JzLlNjcmlwdC5HcnBjLk1lc3NhZ2VzYgZwcm90",
            "bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Microsoft.Azure.WebJobs.Script.Grpc.Messages.Dependency), global::Microsoft.Azure.WebJobs.Script.Grpc.Messages.Dependency.Parser, new[]{ "Enabled" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  /// <summary>
  ///Used to send the managed dependency module for the language worker to download the client module
  /// </summary>
  public sealed partial class Dependency : pb::IMessage<Dependency> {
    private static readonly pb::MessageParser<Dependency> _parser = new pb::MessageParser<Dependency>(() => new Dependency());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<Dependency> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Microsoft.Azure.WebJobs.Script.Grpc.Messages.DependencyReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Dependency() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Dependency(Dependency other) : this() {
      enabled_ = other.enabled_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Dependency Clone() {
      return new Dependency(this);
    }

    /// <summary>Field number for the "enabled" field.</summary>
    public const int EnabledFieldNumber = 1;
    private bool enabled_;
    /// <summary>
    ///Flag indicating if managed dependency is enabled or not 
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Enabled {
      get { return enabled_; }
      set {
        enabled_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as Dependency);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(Dependency other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Enabled != other.Enabled) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Enabled != false) hash ^= Enabled.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (Enabled != false) {
        output.WriteRawTag(8);
        output.WriteBool(Enabled);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Enabled != false) {
        size += 1 + 1;
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(Dependency other) {
      if (other == null) {
        return;
      }
      if (other.Enabled != false) {
        Enabled = other.Enabled;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            Enabled = input.ReadBool();
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
