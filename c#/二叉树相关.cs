using System;
using System.Collections.Generic;

public class TreeNode
{
    public int val;
    public TreeNode left;
    public TreeNode right;
    public TreeNode(int val = 0, TreeNode left = null, TreeNode right = null)
    {
        this.val = val;
        this.left = left;
        this.right = right;
    }
}

public class Program
{
    //翻转二叉树
    public TreeNode InvertTree(TreeNode root)
    {
        if(root == null)return null;
        Console.WriteLine(root.val);
        TreeNode TempNode = root.left;
        root.left = root.right;
        root.right = TempNode;
        InvertTree(root.left);
        InvertTree(root.right);
        return root;
    }

    //前序遍历 根左右
    public TreeNode PreOrder(TreeNode root)
    {
        if(root == null)return null;
        Console.WriteLine(root.val);
        PreOrder(root.left);
        PreOrder(root.right);
        return root;
    }

    //中序遍历 左根右
    public TreeNode MidOrder(TreeNode root)
    {
        if(root == null)return null;
        MidOrder(root.left);
        Console.WriteLine(root.val);
        MidOrder(root.right);
        return root;
    }

    //后序遍历 左右根
    public TreeNode PostOrder(TreeNode root)
    {
        if(root == null)return null;
        PostOrder(root.left);
        PostOrder(root.right);
        Console.WriteLine(root.val);
        return root;
    }

    //层序遍历 逐层遍历，从上到下
    public TreeNode LevelOrder(TreeNode root)
    {
        if(root == null)return null;
        Queue<TreeNode> queue = new Queue<TreeNode>();
        queue.Enqueue(root);
        while(queue.Count > 0)
        {
            TreeNode node = queue.Dequeue();
            Console.WriteLine(node.val);
            if(node.left != null)queue.Enqueue(node.left);
            if(node.right != null)queue.Enqueue(node.right);
        }
        return root;
    }

    public static void Main(string[] args)
    {
        Program program = new Program();
        Console.WriteLine("测试");
        // 构建二叉树
        //       1
        //      / \
        //     2   3
        //    / \   \
        //   4   5   6
        TreeNode root = new TreeNode(1);
        root.left = new TreeNode(2);
        root.right = new TreeNode(3);
        root.left.left = new TreeNode(4);
        root.left.right = new TreeNode(5);
        root.right.right = new TreeNode(6);

        Console.WriteLine("前序遍历（根左右）:");
        program.PreOrder(root);
        Console.WriteLine("\n");
        
        Console.WriteLine("中序遍历（左根右）:");
        program.MidOrder(root);
        Console.WriteLine("\n");
        
        Console.WriteLine("后序遍历（左右根）:");
        program.PostOrder(root);
        Console.WriteLine("\n");
        
        Console.WriteLine("层序遍历（从上到下）:");
        program.LevelOrder(root);
        Console.WriteLine("\n\n");

        Console.WriteLine("翻转二叉树");
        program.InvertTree(root);
    }
}

