using Puhu.Btop.Core.Platform;

namespace Puhu.Btop.Tests.Core;

public class ProcessTreeBuilderTests
{
    [Fact(Timeout = 30000)]
    public void Build_single_root_no_children()
    {
        var parentMap = new Dictionary<int, int> { [1] = 0 };
        var nameMap = new Dictionary<int, string> { [1] = "init" };

        var tree = ProcessTreeBuilder.Build(1, parentMap, nameMap);

        Assert.Equal(1, tree.Pid);
        Assert.Equal("init", tree.Name);
        Assert.Empty(tree.Children);
    }

    [Fact(Timeout = 30000)]
    public void Build_root_with_two_children_sorted_by_pid()
    {
        var parentMap = new Dictionary<int, int>
        {
            [10] = 1, [5] = 1, [1] = 0,
        };
        var nameMap = new Dictionary<int, string>
        {
            [1] = "root", [5] = "sshd", [10] = "bash",
        };

        var tree = ProcessTreeBuilder.Build(1, parentMap, nameMap);

        Assert.Equal(2, tree.Children.Count);
        Assert.Equal(5, tree.Children[0].Pid);
        Assert.Equal("sshd", tree.Children[0].Name);
        Assert.Equal(10, tree.Children[1].Pid);
        Assert.Equal("bash", tree.Children[1].Name);
    }

    [Fact(Timeout = 30000)]
    public void Build_nested_three_levels()
    {
        var parentMap = new Dictionary<int, int>
        {
            [2] = 1, [3] = 2, [1] = 0,
        };
        var nameMap = new Dictionary<int, string>
        {
            [1] = "root", [2] = "shell", [3] = "vim",
        };

        var tree = ProcessTreeBuilder.Build(1, parentMap, nameMap);

        Assert.Single(tree.Children);
        Assert.Single(tree.Children[0].Children);
        Assert.Equal("vim", tree.Children[0].Children[0].Name);
    }

    [Fact(Timeout = 30000)]
    public void Build_limits_depth_to_5()
    {
        // Chain: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7 -> 8
        var parentMap = new Dictionary<int, int>();
        var nameMap = new Dictionary<int, string>();
        for (var i = 1; i <= 8; i++)
        {
            parentMap[i] = i - 1;
            nameMap[i] = $"proc{i}";
        }

        var tree = ProcessTreeBuilder.Build(1, parentMap, nameMap);

        // Walk down the tree and count depth
        var depth = 0;
        var node = tree;
        while (node.Children.Count > 0)
        {
            depth++;
            node = node.Children[0];
        }

        Assert.Equal(5, depth);
    }

    [Fact(Timeout = 30000)]
    public void Build_uses_fallback_name_for_unknown_pid()
    {
        var parentMap = new Dictionary<int, int> { [99] = 1 };
        var nameMap = new Dictionary<int, string> { [99] = "child" };

        var tree = ProcessTreeBuilder.Build(1, parentMap, nameMap);

        Assert.Equal("PID 1", tree.Name);
        Assert.Single(tree.Children);
        Assert.Equal("child", tree.Children[0].Name);
    }

    [Fact(Timeout = 30000)]
    public void Build_handles_empty_maps()
    {
        var tree = ProcessTreeBuilder.Build(1,
            new Dictionary<int, int>(),
            new Dictionary<int, string>());

        Assert.Equal(1, tree.Pid);
        Assert.Equal("PID 1", tree.Name);
        Assert.Empty(tree.Children);
    }

    [Fact(Timeout = 30000)]
    public void BuildChildrenMap_groups_children_correctly()
    {
        var parentMap = new Dictionary<int, int>
        {
            [2] = 1, [3] = 1, [4] = 2,
        };

        var childrenMap = ProcessTreeBuilder.BuildChildrenMap(parentMap);

        Assert.Equal(2, childrenMap.Count);
        Assert.Equal([2, 3], childrenMap[1].OrderBy(x => x));
        Assert.Equal([4], childrenMap[2]);
    }
}
