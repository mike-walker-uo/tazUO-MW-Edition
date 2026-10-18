using System;
using System.Collections.Generic;
using System.Text;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    // Uppercase operators are reserved; ordinary multiword searches stay literal phrases.
    internal sealed class ContainerSearchQuery
    {
        internal enum MatchState { NoMatch, Match, Pending }
        private readonly Node _root;
        internal string Error { get; }
        internal bool IsValid => Error == null;

        private ContainerSearchQuery(Node root, string error) { _root = root; Error = error; }

        internal static ContainerSearchQuery Parse(string text)
        {
            var parser = new Parser(text ?? "");
            Node root = parser.Parse();
            return new ContainerSearchQuery(root, parser.Error);
        }

        internal bool Matches(Item item) => Evaluate(item) == MatchState.Match;

        internal MatchState Evaluate(Item item)
        {
            if (item == null || item.IsDestroyed || !IsValid) return MatchState.NoMatch;
            if (_root == null) return MatchState.Match;
            if (World.OPL.TryGetNameAndData(item.Serial, out string name, out string data))
                return Evaluate(name, data, true);
            return Evaluate(item.Name, item.ItemData.Name, false);
        }

        internal bool Matches(string name, string data) => Evaluate(name, data, true) == MatchState.Match;
        internal MatchState Evaluate(string name, string data, bool propertiesKnown) => !IsValid ? MatchState.NoMatch
            : _root == null ? MatchState.Match : _root.Evaluate(name, data, propertiesKnown);

        internal bool Includes(Item item, bool nonMatches) => item != null && !item.IsDestroyed && IsValid
            && Evaluate(item) == (nonMatches ? MatchState.NoMatch : MatchState.Match);

        private enum Kind { Term, And, Or, Not, Open, Close, End }

        private readonly struct Token
        {
            internal readonly Kind Kind;
            internal readonly string Text;
            internal Token(Kind kind, string text = null) { Kind = kind; Text = text; }
        }

        private sealed class Node
        {
            private readonly Kind _kind;
            private readonly string _term;
            private readonly Node _left, _right;
            internal Node(Kind kind, Node left = null, Node right = null, string term = null)
            { _kind = kind; _left = left; _right = right; _term = term; }

            internal MatchState Evaluate(string name, string data, bool propertiesKnown)
            {
                MatchState left = _left?.Evaluate(name, data, propertiesKnown) ?? MatchState.Pending;
                switch (_kind)
                {
                    case Kind.And:
                        if (left == MatchState.NoMatch) return left;
                        MatchState andRight = _right.Evaluate(name, data, propertiesKnown);
                        return andRight == MatchState.NoMatch ? andRight
                            : left == MatchState.Pending || andRight == MatchState.Pending ? MatchState.Pending : MatchState.Match;
                    case Kind.Or:
                        if (left == MatchState.Match) return left;
                        MatchState orRight = _right.Evaluate(name, data, propertiesKnown);
                        return orRight == MatchState.Match ? orRight
                            : left == MatchState.Pending || orRight == MatchState.Pending ? MatchState.Pending : MatchState.NoMatch;
                    case Kind.Not:
                        return left == MatchState.Pending ? left : left == MatchState.Match ? MatchState.NoMatch : MatchState.Match;
                    default:
                        bool found = (name?.IndexOf(_term, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                            || (data?.IndexOf(_term, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                        return found ? MatchState.Match : propertiesKnown ? MatchState.NoMatch : MatchState.Pending;
                }
            }
        }

        private sealed class Parser
        {
            private readonly List<Token> _tokens = new List<Token>();
            private int _index;
            internal string Error { get; private set; }
            private Kind Current => _index < _tokens.Count ? _tokens[_index].Kind : Kind.End;

            internal Parser(string text)
            {
                var phrase = new StringBuilder();
                void FlushPhrase()
                {
                    string term = phrase.ToString().Trim();
                    if (term.Length != 0) _tokens.Add(new Token(Kind.Term, term));
                    phrase.Clear();
                }
                for (int i = 0; i < text.Length;)
                {
                    char c = text[i];
                    if (char.IsWhiteSpace(c)) { phrase.Append(c); i++; continue; }
                    if (c == '(' || c == ')')
                    {
                        FlushPhrase(); _tokens.Add(new Token(c == '(' ? Kind.Open : Kind.Close)); i++; continue;
                    }
                    if (c == '"')
                    {
                        FlushPhrase();
                        int end = text.IndexOf('"', i + 1);
                        if (end < 0) { Error = "Close the quoted phrase with a double quote."; return; }
                        string term = text.Substring(i + 1, end - i - 1);
                        if (string.IsNullOrWhiteSpace(term)) { Error = "Enter text inside the quotes."; return; }
                        _tokens.Add(new Token(Kind.Term, term)); i = end + 1; continue;
                    }
                    int start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '(' && text[i] != ')' && text[i] != '"') i++;
                    string word = text.Substring(start, i - start);
                    Kind kind = word == "AND" ? Kind.And : word == "OR" ? Kind.Or : word == "NOT" ? Kind.Not : Kind.Term;
                    if (kind == Kind.Term) phrase.Append(word);
                    else { FlushPhrase(); _tokens.Add(new Token(kind)); }
                }
                FlushPhrase();
            }

            internal Node Parse()
            {
                if (Error != null || Current == Kind.End) return null;
                Node root = ParseOr();
                if (Error == null && Current != Kind.End) Error = "Use AND or OR between terms; check parentheses.";
                return root;
            }

            private Node ParseOr()
            {
                Node left = ParseAnd();
                while (Error == null && Current == Kind.Or)
                {
                    _index++;
                    left = new Node(Kind.Or, left, ParseAnd());
                }
                return left;
            }

            private Node ParseAnd()
            {
                Node left = ParseFactor();
                while (Error == null && Current == Kind.And)
                {
                    _index++;
                    left = new Node(Kind.And, left, ParseFactor());
                }
                return left;
            }

            private Node ParseFactor()
            {
                if (Current == Kind.Not)
                {
                    _index++; return new Node(Kind.Not, ParseFactor());
                }
                if (Current == Kind.Open)
                {
                    _index++;
                    Node group = ParseOr();
                    if (Error == null && Current != Kind.Close) Error = "Missing closing parenthesis.";
                    if (Current == Kind.Close) _index++;
                    return group;
                }
                if (Current == Kind.Term) return new Node(Kind.Term, term: _tokens[_index++].Text);
                Error = "Expected a search term, quoted phrase or parenthesized group.";
                return null;
            }
        }
    }
}
