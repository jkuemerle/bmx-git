﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Inedo.Linq;

namespace Inedo.BuildMasterExtensions.Git.Clients
{
    internal sealed class StandardGitClient : GitClientBase
    {
        private static readonly Regex BranchParsingRegex = new Regex(@"refs/heads/(?<branch>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline);
        private string gitExePath;

        public StandardGitClient(IGitSourceControlProvider provider, string gitExePath)
            : base(provider)
        {
            this.gitExePath = gitExePath;
        }

        protected override string GitExePath
        {
            get { return this.gitExePath; }
        }

        public override IEnumerable<string> EnumBranches(IGitRepository repo)
        {
            string stdout = this.ExecuteGitCommand(repo, "ls-remote --heads origin");
            return BranchParsingRegex.Matches(stdout).Cast<Match>()
                .Select(match => match.Groups["branch"].Value);
        }

        public override void UpdateLocalRepo(IGitRepository repo, string branch, string tag)
        {
            /* 
             *  git fetch origin <branch>    | Get all changesets for the specified branch but does not apply them
             *  git reset --hard FETCH_HEAD  | Resets to the HEAD revision and removes commits that haven't been pushed
             *  git clean -dfq               | Remove all non-Git versioned files and directories from the repository directory
             */

            this.ExecuteGitCommand(repo, "fetch", repo.RemoteRepositoryUrl, branch, "--quiet");
            this.ExecuteGitCommand(repo, "reset --hard FETCH_HEAD --quiet");
            this.ExecuteGitCommand(repo, "clean -dfq");
        }

        public override void ApplyTag(IGitRepository repo, string tag)
        {
            // tag the default branch (where -f replaces existing tag) with the value of label
            this.ExecuteGitCommand(repo, "tag -f", tag);
            this.ExecuteGitCommand(repo, "push", repo.RemoteRepositoryUrl, "--tags --quiet");
        }

        public override byte[] GetLastCommit(IGitRepository repo, string branch)
        {
            var rev = new byte[20];
            var revStr = this.ExecuteGitCommand(repo, "log -1", "--pretty=format:%H");
            if (string.IsNullOrEmpty(revStr))
                return rev;

            for (int i = 0; i < rev.Length; i++)
                rev[i] = byte.Parse(revStr.Substring(i * 2, 2), NumberStyles.HexNumber);

            return rev;
        }

        public override void CloneRepo(IGitRepository repo)
        {
            throw new NotImplementedException();
        }

        public override void ValidateConnection()
        {
            foreach (var repo in this.Provider.Repositories)
            {
                if (string.IsNullOrEmpty(repo.RemoteRepositoryUrl))
                    this.ExecuteGitCommand(repo, "log -n 1"); // show commit log, limit to 1 commit
                else
                    this.ExecuteGitCommand(repo, "ls-remote --heads origin"); // list remote branches
            }
        }

        private new string ExecuteGitCommand(IGitRepository repo, string gitCommand, params string[] options)
        {
            var results = base.ExecuteGitCommand(repo, gitCommand, options);
            if (results.ExitCode != 0)
                throw new InvalidOperationException(string.Join(" ", results.Error.ToArray()));

            return string.Join(Environment.NewLine, results.Output.ToArray());
        }
    }
}
