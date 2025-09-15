import React, { useState, useEffect } from 'react';
import './Scoreboard.css';

// Team Summary Component
const TeamSummary = ({ team1, team2 }) => {
  if (!team1 && !team2) return null;

  return (
    <div className="unified-team-header">
      {/* Team 1 */}
      <div className="team-summary-item">
        {team1 && (
          <>
            <img 
              className="team-summary-logo" 
              src={`data:image/png;base64,${team1.ownerLogo}`} 
              alt="Team Logo" 
            />
            <div className="team-summary-score">
              {team1.totalFantasyPoints.toFixed(2)}
            </div>
          </>
        )}
      </div>

      {/* VS Divider */}
      <div className="vs-divider">VS</div>

      {/* Team 2 */}
      <div className="team-summary-item">
        {team2 && (
          <>
            <img 
              className="team-summary-logo" 
              src={`data:image/png;base64,${team2.ownerLogo}`} 
              alt="Team Logo" 
            />
            <div className="team-summary-score">
              {team2.totalFantasyPoints.toFixed(2)}
            </div>
          </>
        )}
      </div>
    </div>
  );
};

// Player Row Component
const PlayerRow = ({ player }) => {
  const getMatchupDisplay = () => {
    if (!player.gameTime) return '';

    const gameDate = new Date(player.gameTime);
    const day = gameDate.toLocaleDateString('en-US', { weekday: 'short' });
    const hour = gameDate.getHours() > 12 ? gameDate.getHours() - 12 : gameDate.getHours();
    const minute = gameDate.getMinutes().toString().padStart(2, '0');
    const amPm = gameDate.getHours() >= 12 ? 'PM' : 'AM';
    const opponentAbbreviation = player.opponentAbbreviation.toUpperCase();
    const gameLocation = player.homeOrAway === 'home' ? 'vs' : '@';

    if (player.gameEnded) {
      return `Final ${player.finalScoreString} ${gameLocation} ${opponentAbbreviation}`;
    } else if (player.gameCanceled) {
      return 'Canceled';
    } else if (player.gameInProgress) {
      return `${player.timeRemaining} ${player.currentScoreString} ${gameLocation} ${opponentAbbreviation}`;
    } else {
      return `${day} ${hour}:${minute}${amPm} ${gameLocation} ${opponentAbbreviation}`;
    }
  };

  const getPlayerPoints = () => {
    if (!player.gameEnded && !player.gameCanceled && !player.gameInProgress) {
      return '-';
    }
    return player.points.toFixed(2);
  };

  return (
    <div className={`player-row ${player.gameInProgress ? 'in-progress' : ''}`}>
      <img 
        className="player-headshot" 
        src={player.headshot} 
        alt="Player headshot" 
      />
      
      <div className="player-info">
        <div className="player-name">{player.name}</div>
        <div className="player-details">
          {player.teamAbbreviation.toUpperCase()} - {player.truePosition}<br />
          <span className="mobile-hide-details">
            {player.gameCanceled ? (
              <span style={{color: '#FF7F7F'}}>{getMatchupDisplay()}</span>
            ) : (
              getMatchupDisplay()
            )}
          </span>
        </div>
      </div>
      
      <div className={`player-points ${player.gameInProgress ? 'in-progress' : ''}`}>
        {getPlayerPoints()}
      </div>
    </div>
  );
};

// Team Players Section Component
const TeamPlayersSection = ({ team, positions }) => {
  if (!team) return null;

  if (team.players.length === 0) {
    return (
      <div className="team-players-section">
        <div className="select-team-link">
          <a href={`/SelectTeam2/Index?week=${team.week}&ownerId=${team.ownerId}`} className="btn btn-primary">
            Click here to select this week's roster
          </a>
        </div>
      </div>
    );
  }

  return (
    <div className="team-players-section">
      {team.players.map((player, index) => (
        <PlayerRow key={index} player={player} />
      ))}
    </div>
  );
};

// Position Labels Component
const PositionLabels = () => {
  const positions = ['QB', 'RB', 'RB', 'WR', 'WR', 'FLEX', 'TE', 'K', 'DEF'];
  
  return (
    <div className="position-labels-section">
      {positions.map((position, index) => (
        <div key={index} className="position-column">
          {position}
        </div>
      ))}
    </div>
  );
};

// Week Selection Component
const WeekSelection = ({ weeks, selectedWeek, onWeekChange }) => {
  const handleWeekChange = (e) => {
    onWeekChange(e.target.value);
  };

  return (
    <div className="scoreboard-header">
      <div className="row justify-content-center">
        <div className="col-12 col-md-6">
          <div className="week-selection-container">
            <label className="week-selection-label">Select Week</label>
            <select 
              className="form-control" 
              value={selectedWeek} 
              onChange={handleWeekChange}
            >
              {weeks.map((week, index) => (
                <option key={index} value={week.value}>
                  {week.text}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>
    </div>
  );
};

// Main Scoreboard Component
const Scoreboard = () => {
  const [teams, setTeams] = useState([]);
  const [weeks, setWeeks] = useState([]);
  const [selectedWeek, setSelectedWeek] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Fetch available weeks
  useEffect(() => {
    const fetchWeeks = async () => {
      try {
        const response = await fetch('/api/scoreboard/weeks');
        if (!response.ok) throw new Error('Failed to fetch weeks');
        const weeksData = await response.json();
        setWeeks(weeksData);
        
        // Set the selected week to the first selected week from the server
        const defaultWeek = weeksData.find(w => w.selected);
        if (defaultWeek) {
          setSelectedWeek(defaultWeek.value);
        }
      } catch (err) {
        setError('Failed to load weeks: ' + err.message);
      }
    };

    fetchWeeks();
  }, []);

  // Fetch teams when selectedWeek changes
  useEffect(() => {
    if (!selectedWeek) return;

    const fetchTeams = async () => {
      setLoading(true);
      try {
        const response = await fetch(`/api/scoreboard/teams/${selectedWeek}`);
        if (!response.ok) throw new Error('Failed to fetch teams');
        const teamsData = await response.json();
        setTeams(teamsData);
      } catch (err) {
        setError('Failed to load teams: ' + err.message);
      } finally {
        setLoading(false);
      }
    };

    fetchTeams();
  }, [selectedWeek]);

  const handleWeekChange = async (week) => {
    setSelectedWeek(week);
    
    // Update the server about the week change
    try {
      await fetch(`/api/scoreboard/week/${week}`, { method: 'POST' });
    } catch (err) {
      console.error('Failed to update week on server:', err);
    }
  };

  if (error) {
    return <div className="alert alert-danger">{error}</div>;
  }

  const team1 = teams[0] || null;
  const team2 = teams[1] || null;

  return (
    <div className="scoreboard-container">
      <div className="scoreboard-main-card">
        {/* Week Selection Header */}
        <WeekSelection 
          weeks={weeks}
          selectedWeek={selectedWeek}
          onWeekChange={handleWeekChange}
        />

        {/* Unified Team Summary Header */}
        <div className="team-summary-section">
          <TeamSummary team1={team1} team2={team2} />
        </div>

        {/* Main Scoreboard Content */}
        <div className="scoreboard-content">
          {loading ? (
            <div className="text-center p-4">
              <div className="spinner-border" role="status">
                <span className="sr-only">Loading...</span>
              </div>
            </div>
          ) : (
            <div className="row no-gutters">
              {/* Team 1 Players */}
              <div className="col-5">
                <TeamPlayersSection team={team1} />
              </div>

              {/* Position Labels Column */}
              <div className="col-2">
                <PositionLabels />
              </div>

              {/* Team 2 Players */}
              <div className="col-5">
                <TeamPlayersSection team={team2} />
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default Scoreboard;