import React, { useEffect } from 'react';

class SearchBar extends React.Component {
    constructor(props) {
        super(props);
        this.handleFilterTextChange = this.handleFilterTextChange.bind(this);
    }

    handleFilterTextChange(e) {
        this.props.onFilterTextChange(e.target.value);
    }

    render() {
        return (
            <form>
                <input type="text" placeholder="Search..." value={this.props.filterText} onChange={this.handleFilterTextChange} />
            </form>
        );
    }
}

export default SearchBar;