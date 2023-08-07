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
        const MAX_AVAILABLE_PLAYERS_TO_DISPLAY = 10;
        const availablePlayers = this.state.availablePlayers;
        const filterText = this.state.filterText;

        // player count to determine if we've displayed our preferred number of players on the screen
        let playerCount = 0;

        const availablePlayersMap = availablePlayers.map((player, index) => {
            // get the index of the filtered player in the available players list so we can render the correct player
            if (player.name.toLowerCase().includes(filterText.toLowerCase()) && playerCount < MAX_AVAILABLE_PLAYERS_TO_DISPLAY) {
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
    let isValidRoster = true;

    // a valid roster includes QB, WR, WR, RB, RB, Flex, TE, K, DEF
    let numQbs = 0;
    let numWrs = 0;
    let numRbs = 0;
    let numTes = 0;
    let numKs = 0;
    let numDefs = 0;

    // check if there are 9 players selected for the roster
    if (roster.length === 9) {

        for (let i = 0; i < roster.length; i++) {

            switch (roster[i].position) {
                case 'QB':
                    if (numQbs < 1) {
                        numQbs++;
                    }
                    else {
                        // a 2nd QB is trying to be added, so return false and skip the rest of the roster
                        return false;
                    }
                    break;

                case 'WR':
                    // if there are not yet 2 WRs or if a 3rd WR is being added as a flex and there aren't already 3 RBs or 2 TEs
                    if ((numWrs < 2) || ((numWrs === 2) && (numRbs < 3) && (numTes < 2))) {
                        numWrs++;
                    }
                    else {
                        // a 3rd WR is trying to be added as a FLEX and there is already a 3rd RB or 2nd TE as a FLEX
                        return false;
                    }
                    break;

                case 'RB':
                    // if there are not yet 2 RBs or if a 3rd RB is being added as a flex and there aren't already 3 WRs or 2 TEs
                    if ((numRbs < 2) || ((numRbs === 2) && (numWrs < 3) && (numTes < 2))) {
                        numRbs++;
                    }
                    else {
                        // a 3rd WR is trying to be added as a FLEX and there is already a 3rd RB or 2nd TE as a FLEX
                        return false;
                    }
                    break;

                case 'TE':
                    // if there is not yet 1 TE or if a 2nd TE is being added as a flex and there aren't already 3 WRs or 3 WRs
                    if ((numTes < 1) || ((numTes === 1) && (numRbs < 3) && (numWrs < 3))) {
                        numTes++;
                    }
                    else {
                        // a 3rd WR is trying to be added as a FLEX and there is already a 3rd RB or 2nd TE as a FLEX
                        return false;
                    }
                    break;

                case 'K':
                    if (numKs < 1) {
                        numKs++;
                    }           
                    else {
                        return false;
                    }     
                    break;

                case 'DEF':
                    if (numDefs < 1) {
                        numDefs++;
                    }
                    else {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }
    }
    else {
        isValidRoster = false;
    }

    return isValidRoster;
}