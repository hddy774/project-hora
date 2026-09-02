namespace Chronicles.Simulation;

/// <summary>
/// 인물별로 하나의 다음 행동만 보유하는 고정 용량 indexed min-heap이다.
/// 전투 상대 재예약 때 생기던 stale PriorityQueue 항목과 tuple 비교 비용을 제거한다.
/// </summary>
internal sealed class AgentDecisionSchedule
{
    private readonly int[] _agents;
    private readonly long[] _ticks;
    private readonly int[] _positions;
    private int _count;

    public AgentDecisionSchedule(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _agents = new int[population];
        _ticks = new long[population];
        _positions = new int[population];
        Array.Fill(_positions, -1);
    }

    public int Count => _count;

    public void Schedule(int agent, long tick)
    {
        ValidateAgent(agent);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var position = _positions[agent];
        if (position < 0)
        {
            position = _count++;
            _agents[position] = agent;
            _ticks[position] = tick;
            _positions[agent] = position;
            MoveUp(position);
            return;
        }

        var previousTick = _ticks[position];
        if (previousTick == tick)
        {
            return;
        }

        _ticks[position] = tick;
        if (tick < previousTick)
        {
            MoveUp(position);
        }
        else
        {
            MoveDown(position);
        }
    }

    public bool Remove(int agent)
    {
        ValidateAgent(agent);
        var position = _positions[agent];
        if (position < 0)
        {
            return false;
        }

        RemoveAt(position);
        return true;
    }

    public bool TryPeek(out int agent, out long tick)
    {
        if (_count == 0)
        {
            agent = -1;
            tick = 0;
            return false;
        }

        agent = _agents[0];
        tick = _ticks[0];
        return true;
    }

    public int Dequeue()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("다음 행동 일정이 비어 있습니다.");
        }

        var agent = _agents[0];
        RemoveAt(0);
        return agent;
    }

    private void RemoveAt(int position)
    {
        var removedAgent = _agents[position];
        _positions[removedAgent] = -1;
        var lastPosition = --_count;
        if (position == lastPosition)
        {
            return;
        }

        var movedAgent = _agents[lastPosition];
        var movedTick = _ticks[lastPosition];
        _agents[position] = movedAgent;
        _ticks[position] = movedTick;
        _positions[movedAgent] = position;

        var parent = (position - 1) >> 1;
        if (position > 0 && IsEarlier(position, parent))
        {
            MoveUp(position);
        }
        else
        {
            MoveDown(position);
        }
    }

    private void MoveUp(int position)
    {
        while (position > 0)
        {
            var parent = (position - 1) >> 1;
            if (!IsEarlier(position, parent))
            {
                break;
            }

            Swap(position, parent);
            position = parent;
        }
    }

    private void MoveDown(int position)
    {
        while (true)
        {
            var left = (position << 1) + 1;
            if (left >= _count)
            {
                return;
            }

            var right = left + 1;
            var earlier = right < _count && IsEarlier(right, left) ? right : left;
            if (!IsEarlier(earlier, position))
            {
                return;
            }

            Swap(position, earlier);
            position = earlier;
        }
    }

    private bool IsEarlier(int left, int right)
    {
        var tickComparison = _ticks[left].CompareTo(_ticks[right]);
        return tickComparison < 0
            || (tickComparison == 0 && _agents[left] < _agents[right]);
    }

    private void Swap(int left, int right)
    {
        (_agents[left], _agents[right]) = (_agents[right], _agents[left]);
        (_ticks[left], _ticks[right]) = (_ticks[right], _ticks[left]);
        _positions[_agents[left]] = left;
        _positions[_agents[right]] = right;
    }

    private void ValidateAgent(int agent)
    {
        if ((uint)agent >= (uint)_positions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(agent));
        }
    }
}
