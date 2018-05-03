﻿using SudokuSolver.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SudokuSolver
{
    public partial class MainWindow : Form
    {
        Stopwatch stopwatch;
        Solver solver;

        public MainWindow()
        {
            InitializeComponent();
            solveButton.Enabled = false;
            statusLabel.Text = "";
        }

        private void SolveButton_Click(object sender, EventArgs e)
        {
            solveButton.Enabled = false;
            stopwatch = new Stopwatch();
            var bw = new BackgroundWorker();
            bw.DoWork += solver.DoWork;
            bw.RunWorkerCompleted += Solver_Finished;
            stopwatch.Start();
            bw.RunWorkerAsync();
        }

        private void Solver_Finished(object sender, RunWorkerCompletedEventArgs e)
        {
            stopwatch.Stop();
            sudokuBoard.Invalidate();
            logTextBox.Text = (string)e.Result;
            statusLabel.Text = string.Format("Solver finished in {0} seconds.", stopwatch.Elapsed.TotalSeconds);
        }

        // Return true if puzzle was loaded correctly
        private bool LoadPuzzle(string filename)
        {
            string[] filelines = File.ReadAllLines(filename);
            if (filelines.Length != 9) return false;
            var board = Utils.CreateJaggedArray<byte[][]>(9,9);
            for (byte i = 0; i < 9; i++)
            {
                string[] split = filelines[i].Split(',');
                if (split.Length != 9) return false;
                for (byte j = 0; j < 9; j++)
                {
                    if (!string.IsNullOrEmpty(split[j]))
                    {
                        if (split[j].Length > 1) return false;
                        board[j][i] = byte.Parse(split[j]);
                    }
                }
            }

            solver = new Solver(board, sudokuBoard);
            return true;
        }

        private void OpenPuzzle(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog
            {
                Title = "Open Sudoku Puzzle",
                Filter = "TXT files|*.txt",
                InitialDirectory = Path.GetFullPath(Directory.GetCurrentDirectory() + "\\..\\Puzzles")
            };
            if (d.ShowDialog() != DialogResult.OK) return;

            if (LoadPuzzle(d.FileName))
            {
                solveButton.Enabled = true;
                logTextBox.Text = "";
            }
            else
            {
                MessageBox.Show("Invalid puzzle data.");
            }
        }
    }
}
