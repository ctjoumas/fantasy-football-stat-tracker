import React, { useEffect } from 'react';
import ReactDOM from 'react-dom/client';
import { Stack, IStackStyles, IStackTokens } from '@fluentui/react/lib/Stack';
//import { Stack, IStackTokens } from '@fluentui/react';
import { PrimaryButton, CompoundButton, Modal, IconButton } from '@fluentui/react';
import './index.css';
import SearchBar from './SearchBar';

function Submit(props) {
    return (
        <PrimaryButton text="submit" onClick={() => props.onClick()}></PrimaryButton>
    );
}

function Player(props) {
    return (
        <Stack onClick={() => props.onClick()}>
            <CompoundButton secondaryText={props.position}>
                {props.name}
            </CompoundButton>
        </Stack>
    );
}

const availablePlayers = [
    {"name": "Tom Brady", "position": "QB", "id": 1, "teamAbbreviation": "ne"},
    {"name": "Josh Allen", "position": "QB", "id": 1, "teamAbbreviation": "buf"},
    {"name": "Patrick Mahomes", "position": "QB", "id": 1, "teamAbbreviation": "kc"},
    {"name": "Terry McLaurin", "position": "WR", "id": 2, "teamAbbreviation": "was"},
    {"name": "Justin Jefferson", "position": "WR", "id": 1, "teamAbbreviation": "min"},
    {"name": "Davante Adams", "position": "WR", "id": 1, "teamAbbreviation": "oak"},
    {"name": "Derrick Henry", "position": "RB", "id": 1, "teamAbbreviation": "ten"},
    {"name": "Dalvin Cook", "position": "RB", "id": 3, "teamAbbreviation": "min"},
    {"name": "Josh Jacobs", "position": "RB", "id": 1, "teamAbbreviation": "oak"},
    {"name": "Travis Kelce", "position": "TE", "id": 4, "teamAbbreviation": "kc"},
    {"name": "George Kittle", "position": "TE", "id": 1, "teamAbbreviation": "sf"},
    {"name": "Mark Andrews", "position": "TE", "id": 1, "teamAbbreviation": "bal"},
    {"name": "oakland", "position": "DEF", "id": 5, "teamAbbreviation": "oak"},
    {"name": "Justin Tucker", "position": "K", "id": 1, "teamAbbreviation": "bal"},
    {"name": "Evan McPherson", "position": "K", "id": 1, "teamAbbreviation": "cin"},
    {"name": "baltimore", "position": "DEF", "id": 1, "teamAbbreviation": "bal"}]

class DraftBoard extends React.Component {
    constructor(props) {
        super(props);
        this.state = {
            availablePlayers: availablePlayers,
            selectedPlayers: [],
            filterText: ''
        };

        this.handleFilterTextChange = this.handleFilterTextChange.bind(this);
    }

    handleFilterTextChange(filterText) {
        this.setState({ filterText: filterText });
    }

    handleAvailablePlayerClick(i) {
        // the max roster size is 9, so prevent any more than that from being added
        if (this.state.selectedPlayers.length < 9)
        {
            const availablePlayers = this.state.availablePlayers.slice();
            const selectedPlayers = this.state.selectedPlayers.slice();

            // add this player to the selected players list
            selectedPlayers.push(availablePlayers[i]);

            // remove this player from the available player list and add to the selected player list
            availablePlayers.splice(i, 1);

            this.setState({
                availablePlayers: availablePlayers,
                selectedPlayers: selectedPlayers,
            });
        }
    }

    handleSelectedPlayerClick(i) {
        const availablePlayers = this.state.availablePlayers.slice();
        const selectedPlayers = this.state.selectedPlayers.slice();

        // add this player to the available players list
        availablePlayers.push(selectedPlayers[i]);

        // remove this player from the available player list and add to the selected player list
        selectedPlayers.splice(i, 1);

        this.setState({
           availablePlayers: availablePlayers,
           selectedPlayers: selectedPlayers,
        }); 
    }

    renderAvailablePlayer(i) {
        return(
            <Player
                name={this.state.availablePlayers[i].name}
                position={this.state.availablePlayers[i].position}
                onClick={() => this.handleAvailablePlayerClick(i)}
            />
        );
    }

    renderSelectedPlayer(i) {
        return(
            <Player
                name={this.state.selectedPlayers[i].name}
                position={this.state.selectedPlayers[i].position}
                onClick={() => this.handleSelectedPlayerClick(i)}
            />
        );
    }

    handleSubmitRoster() {
        const selectedPlayers = this.state.selectedPlayers;

        const isValidRoster = validateRoster(selectedPlayers);

        if (isValidRoster) {
            alert('valid');
        }
        else {
            alert('invalid');
        }
    }

    render() {
        const availablePlayers = this.state.availablePlayers;
        const filterText = this.state.filterText;

        // player count to determine if we've displayed our preferred number of players on the screen (10 in this case)
        let playerCount = 0;

        const availablePlayersMap = availablePlayers.map((player, index) => {
            // get the index of the filtered player in the available players list so we can render the correct player
            if (player.name.toLowerCase().includes(filterText.toLowerCase()) && playerCount < 10) {
                playerCount++;

                return (                
                    <tr>
                        <td>
                            {this.renderAvailablePlayer(index)}
                        </td>
                    </tr>
                );
            }
            else {
                return null;
            }
        });

        const selectedPlayers = this.state.selectedPlayers;
        const selectedPlayersMap = selectedPlayers.map((player, index) => {
            return (
                <tr>
                    <td>
                        {this.renderSelectedPlayer(index)}
                    </td>
                </tr>
            );
        });

        return (
            <table align='center'>
                <tbody>
                    <tr>
                        <td>
                            <SearchBar filterText={this.state.filterText} onFilterTextChange={this.handleFilterTextChange} />
                        </td>
                    </tr>
                    <tr style={{'height': '750px'}}>
                        <td valign='top'>
                            <table>
                                <tbody>
                                    <tr>
                                        <th>Available Players</th>
                                    </tr>
                                    {availablePlayersMap}
                                </tbody>
                            </table>
                        </td>
                        <td valign='top'>
                            <table>
                                <tbody>
                                    <tr>
                                        <th>Selected Players</th>
                                    </tr>
                                    {selectedPlayersMap}
                                </tbody>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td colSpan='2' align='right'>
                            <Submit onClick={() => this.handleSubmitRoster()} />
                        </td>
                    </tr>
                </tbody>
            </table>
            
        );
    }
}

// ========================================

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(<DraftBoard />);

function validateRoster(roster) {
    let isValidRoster = false;

    for (let i = 0; i < roster.length; i++) {
        if (roster[i].name.toLowerCase() === 'tom brady') {
            isValidRoster = true;
        }
    }

    return isValidRoster;
}