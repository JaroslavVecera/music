﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Muslib.Chords
{
    class QualityMemberParser : IQualityMemberParser
    {
        enum TokenType
        {
            Sus,
            Add,
            Omit,
            Acc,
            Dim,
            Aug,
            Num,
            Maj,
            Min
        }

        class Token
        {
            public TokenType TokenType { get; set; }
            public int Value { get; set; } = 0;

            public bool IsMod()
            {
                return TokenType == TokenType.Maj
                    || TokenType == TokenType.Min
                    || TokenType == TokenType.Dim
                    || TokenType == TokenType.Aug;
            }
        }

        int _index = 0;
        string _expression = "";
        bool _canExtend = true;
        int _lastExt = 0;
        bool IsEnd { get { return _index == _expression.Length; } }

        public List<QualityMember> Parse(string expression)
        {
            _expression = expression;
            _index = 0;
            _canExtend = true;
            _lastExt = 0;
            List<QualityMember> res = new List<QualityMember>();
            SkipWhitespaces();
            while (!IsEnd)
            {
                Token tok = GetNextToken();
                QualityMember member = null;
                if (tok == null)
                    return null;
                else if (tok.TokenType == TokenType.Num || tok.TokenType == TokenType.Acc)
                    member = CreateExtentionOrAltMember(tok);
                else if (tok.IsMod())
                    member = CreateModifierMember(tok);
                else if (tok.TokenType == TokenType.Add || tok.TokenType == TokenType.Omit)
                    member = CreateAddOrOmit(tok);
                else if (tok.TokenType == TokenType.Sus)
                    member = CreateSus(tok);
                if (member == null)
                    return null;
                res.Add(member);
                SkipWhitespaces();
            }
            return res;
        }

        QualityMember CreateSus(Token tok)
        {
            _canExtend = false;
            SkipWhitespaces();
            if (IsEnd || (tok = GetNextToken()).TokenType != TokenType.Num || (tok.Value != 2 && tok.Value != 4))
                return null;
            return new SuspendedMember((Sus)tok.Value);
        }

        QualityMember CreateExtentionOrAltMember(Token tok)
        {
            int acc = 0;
            if (tok.TokenType == TokenType.Acc)
            {
                acc = tok.Value;
                SkipWhitespaces();
                if (IsEnd || (tok = GetNextToken()).TokenType != TokenType.Num)
                    return null;
            }
            if (!_canExtend || tok.Value <= _lastExt  || (acc > 0 && tok.Value < 6))
                return CreateAltMember(acc, tok);
            else
                return CreateExtentionMember(acc, tok);
        }

        QualityMember CreateExtentionMember(int acc, Token tok)
        {
            _lastExt = tok.Value;
            if (!Enum.GetValues<Extention>().Cast<int>().ToList().Contains(tok.Value))
                return null;
            return new ExtentionMember((Extention)tok.Value, (Accidental)acc);
        }

        QualityMember CreateAltMember(int acc, Token tok)
        {
            _canExtend = false;
            if (acc == 0 || tok.Value < 1 || tok.Value > 13)
                return null;
            return new AltMember(tok.Value, (NonzeroAccidental)acc);
        }

        QualityMember CreateModifierMember(Token tok)
        {
            switch (tok.TokenType)
            {
                case TokenType.Maj:
                    return new ModifierMember(Modifier.Major);
                case TokenType.Min:
                    return new ModifierMember(Modifier.Minor);
                case TokenType.Dim:
                    return new ModifierMember(Modifier.Diminished);
                default:
                    return new ModifierMember(Modifier.Augmented);
            }
        }

        QualityMember CreateAddOrOmit(Token tok)
        {
            _canExtend = false;
            bool add = tok.TokenType == TokenType.Add;
            int acc = 0;
            SkipWhitespaces();
            if (IsEnd || ((tok = GetNextToken()).TokenType != TokenType.Acc) && tok.TokenType != TokenType.Num)
                return null;
            if (tok.TokenType == TokenType.Acc)
            {
                acc = tok.Value;
                SkipWhitespaces();
                if (IsEnd || (tok = GetNextToken()).TokenType != TokenType.Num)
                    return null;
            }
            if (add)
                return new Add(tok.Value, (Accidental)acc);
            else
                return new Omit(tok.Value, (Accidental)acc);
        }

        Token GetNextToken()
        {
            SkipWhitespaces();
            char c = _expression[_index++];
            if (char.IsNumber(c))
                return GetNumber(c);
            else if (c == 'b' || c == '#')
                return GetAccidental(c);
            else if (c == '+')
                return new Token() { TokenType = TokenType.Aug };
            else if (c == '-')
                return new Token() { TokenType = TokenType.Dim };
            else if (char.ToLower(c) == 'o')
                return GetOToken();
            else if (char.ToLower(c) == 'm')
                return GetMinMaj(c);
            else if (char.ToLower(c) == 'a')
                return GetAToken();
            else if (char.ToLower(c) == 'd')
                return GetDim();
            else if (char.ToLower(c) == 's')
                return GetSus();
            else
                return null;
        }

        void SkipWhitespaces()
        {
            while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
                _index++;
        }

        Token GetNumber(char c)
        { 
            int i = c - '0';
            if (!IsEnd && char.IsNumber(c = _expression[_index]) && i == 1)
            {
                i *= 10;
                i += _expression[_index] - '0';
                _index++;
            }
            return new Token() { TokenType = TokenType.Num, Value = i };
        }

        Token GetAccidental(char c)
        {
            int val = (c == '#') ? 1 : -1;
            if (!IsEnd && _expression[_index] == c)
            {
                _index *= 2;
                val++;
            }
            return new Token() { TokenType = TokenType.Acc, Value = val };
        }

        Token GetOToken()
        {
            if (_index + 3 <= _expression.Length 
                && char.ToLower(_expression[_index]) == 'm'
                && char.ToLower(_expression[_index + 1]) == 'i'
                && char.ToLower(_expression[_index + 2]) == 't')
            {
                _index += 3;
                return new Token() { TokenType = TokenType.Omit };
            }
            else
            {
                return new Token() { TokenType = TokenType.Dim };
            }
        }

        Token GetMinMaj(char c)
        {
            bool maj = true;
            if (_index + 2 <= _expression.Length && char.ToLower(_expression[_index]) == 'a' && char.ToLower(_expression[_index + 1]) == 'j')
                _index += 2;
            else if (_index + 2 <= _expression.Length && char.ToLower(_expression[_index]) == 'i' && char.ToLower(_expression[_index + 1]) == 'n')
            {
                _index += 2;
                maj = false;
            }
            else
                maj = c == 'M';
            return new Token() { TokenType = maj ? TokenType.Maj : TokenType.Min };
        }

        Token GetAToken()
        {
            if (_index + 2 <= _expression.Length && char.ToLower(_expression[_index]) == 'd' && char.ToLower(_expression[_index + 1]) == 'd')
            {
                _index += 2;
                return new Token() { TokenType = TokenType.Add };
            }
            else if (_index + 2 <= _expression.Length && char.ToLower(_expression[_index]) == 'u' && char.ToLower(_expression[_index + 1]) == 'g')
            {
                _index += 2;
                return new Token() { TokenType = TokenType.Aug };
            }
            else
                return null;
        }

        Token GetDim()
        {
            if (_index + 2 <= _expression.Length && char.ToLower(_expression[_index]) == 'i' && char.ToLower(_expression[_index + 1]) == 'm')
            {
                _index += 2;
                return new Token() { TokenType = TokenType.Dim };
            }
            else
                return null;
        }

        Token GetSus()
        {
            if (_index + 2 <= _expression.Length && char.ToLower(_expression[_index]) == 'u' && char.ToLower(_expression[_index + 1]) == 's')
            {
                _index += 2;
                return new Token() { TokenType = TokenType.Sus };
            }
            else
                return null;
        }
    }
}
