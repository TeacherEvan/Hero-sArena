using System;
using Xunit;
using FluentAssertions;
using HeroArena;
using Godot;
using System.Reflection;
using System.Runtime.Serialization;

namespace HeroArena.Tests.Core;

[Trait("Category", "GodotRuntime")]
public class InputBufferTests
{
    private InputBuffer CreateBuffer()
    {
#pragma warning disable SYSLIB0050
        var buffer = (InputBuffer)FormatterServices.GetUninitializedObject(typeof(InputBuffer));
#pragma warning restore SYSLIB0050

        var structType = typeof(InputBuffer).GetNestedType("BufferedAction", BindingFlags.NonPublic)!;
        var bufferArray = Array.CreateInstance(structType, 32);

        typeof(InputBuffer).GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(buffer, bufferArray);
        typeof(InputBuffer).GetField("_bufferCount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(buffer, 0);
        typeof(InputBuffer).GetField("_frameCounter", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(buffer, 0);

        return buffer;
    }

    private StringName CreateFakeStringName()
    {
#pragma warning disable SYSLIB0050
        return (StringName)FormatterServices.GetUninitializedObject(typeof(StringName));
#pragma warning restore SYSLIB0050
    }

    [Fact]
    public void BufferAction_AddsActionToBuffer()
    {
        var buffer = CreateBuffer();
        var action = CreateFakeStringName();

        buffer.BufferAction(action);

        buffer.IsBuffered(action).Should().BeTrue();
    }

    [Fact]
    public void ConsumeAction_RemovesActionAndReturnsTrue()
    {
        var buffer = CreateBuffer();
        var action = CreateFakeStringName();

        buffer.BufferAction(action);

        bool consumed = buffer.ConsumeAction(action);

        consumed.Should().BeTrue();
        buffer.IsBuffered(action).Should().BeFalse();
    }

    [Fact]
    public void ConsumeAction_WhenNotBuffered_ReturnsFalse()
    {
        var buffer = CreateBuffer();
        var action = CreateFakeStringName();

        bool consumed = buffer.ConsumeAction(action);

        consumed.Should().BeFalse();
    }

    [Fact]
    public void ProcessBuffer_RemovesExpiredActions()
    {
        var buffer = CreateBuffer();
        var action = CreateFakeStringName();

        buffer.BufferAction(action);
        buffer.IsBuffered(action).Should().BeTrue();

        // Simulate frames passing
        var field = typeof(InputBuffer).GetField("_frameCounter", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(buffer, 6); // BUFFER_FRAMES is 6

        // Invoke ProcessBuffer
        var method = typeof(InputBuffer).GetMethod("ProcessBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(buffer, null);

        buffer.IsBuffered(action).Should().BeFalse();
    }

    [Fact]
    public void BufferAction_WhenBufferFull_DoesNotCrash()
    {
        var buffer = CreateBuffer();
        var action = CreateFakeStringName();

        // Max buffer size is 32
        for (int i = 0; i < 32; i++)
        {
            buffer.BufferAction(action);
        }

        // Verify buffer is full by reading internal count
        var countField = typeof(InputBuffer).GetField("_bufferCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
        int count = (int)countField.GetValue(buffer)!;
        count.Should().Be(32);

        // Add 33rd item, should not throw IndexOutOfRangeException
        Action addOverLimit = () => buffer.BufferAction(action);
        addOverLimit.Should().NotThrow();

        // Count should still be 32
        count = (int)countField.GetValue(buffer)!;
        count.Should().Be(32);
    }
}
